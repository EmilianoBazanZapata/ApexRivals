#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Garage.Configuration;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.VehicleSelection.Runtime;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator CircuitCars_GaragePreviewDisplaysEveryCatalogCar()
        {
            var original = _bootstrapper.Context;
            var engine = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/EngineUpgradeDefinition.asset").CreateData();
            var handling = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/HandlingUpgradeDefinition.asset").CreateData();
            var selection = new VehicleSelectionService(original.VehicleCatalog, original.SelectedVehicleState,
                new PlayerProfileSaveCheckpoint(original.SaveService), original.ProgressionState, engine, handling);
            var garage = new GarageService(original.ProgressionState, engine, handling, () => selection.CurrentSelectedDefinition.BasePerformanceStats);
            var context = new ApplicationContext(_flow, original.ProgressionState, original.SelectedVehicleState, original.SaveService,
                original.VehicleCatalog, selection, garage, new ApexRivals.Settings.Runtime.SettingsService(new MemorySettingsStorage(), new MemoryDisplaySettings()),
                original.RaceProgressionService, original.PauseCoordinator);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_bootstrapper, context);
            _installAllContentScenes = true;
            var exit = Installer.Session.ReturnToMainMenu();
            yield return WaitForTask(exit);
            Assert.That(exit.Result.Succeeded, Is.True, exit.Result.Message);
            var open = _flow.OpenGarage();
            yield return WaitForTask(open);
            Assert.That(open.Result.Succeeded, Is.True, open.Result.Message);
            var preview = FindInScene<GarageVehiclePreviewController>("Garage");
            Assert.That(preview, Is.Not.Null);
            var data = new SerializedObject(preview);
            var anchor = (Transform)data.FindProperty("previewAnchor").objectReferenceValue;
            foreach (var id in new[] { "vanguard", "striker", "sunaru", "borshe911", "aduo", "fernedenz", "massalini", "fehhahi" })
            {
                preview.ShowVehicle(id);
                yield return null;
                yield return null;
                var instance = anchor.GetComponentInChildren<Rigidbody>();
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.isKinematic, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(instance.transform.Find("VisualRoot/Body").GetComponent<MeshFilter>().sharedMesh), Does.EndWith(id + "_Body.asset"));
            }
            ScreenCapture.CaptureScreenshot("graphify-out/car-garage-fehhahi.png");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CircuitCars_AllEightSpawnGroundAccelerateAndSynchronizeTheirWheels()
        {
            foreach (var id in new[] { "vanguard", "striker", "sunaru", "borshe911", "aduo", "fernedenz", "massalini", "fehhahi" })
            {
                _bootstrapper.Context.SelectedVehicleState.Commit(id);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, id + ": " + retry.Result.Message);
                var deadline = Time.realtimeSinceStartup + 7;
                while (Installer.Session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
                var body = Installer.Session.PlayerVehicleRigidbody;
                var controller = body.GetComponent<WheelArcadeVehicleController>();
                Assert.That(AssetDatabase.GetAssetPath(body.transform.Find("VisualRoot/Body").GetComponent<MeshFilter>().sharedMesh), Does.EndWith(id + "_Body.asset"));
                var wheels = body.GetComponentsInChildren<WheelCollider>();
                var contacts = 0;
                foreach (var wheel in wheels) if (wheel.isGrounded) contacts++;
                Assert.That(contacts, Is.GreaterThanOrEqualTo(2), id + " must rest on its wheels, not its chassis.");
                var start = body.position;
                var driving = new CircuitCarTestInput();
                body.GetComponent<DrivingInputGate>().Configure(driving, true);
                for (var i = 0; i < 65; i++) yield return new WaitForFixedUpdate();
                Assert.That(controller.CurrentForwardSpeed, Is.GreaterThan(0.5f), id + " must accelerate from throttle input.");
                Assert.That(Vector3.Distance(start, body.position), Is.GreaterThan(0.5f));
                driving.Value = new DrivingInput(0.5f, 0, 0.2f, false, false);
                for (var i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
                yield return null;
                foreach (var suffix in new[] { "FL", "FR", "RL", "RR" })
                {
                    var wheel = body.transform.Find("WheelCollider_" + suffix).GetComponent<WheelCollider>();
                    wheel.GetWorldPose(out var position, out _);
                    var visual = body.transform.Find("VisualRoot/WheelVisualRoot_" + suffix);
                    Assert.That(Vector3.Distance(position, visual.position), Is.LessThan(0.15f), id + " wheel " + suffix);
                }
                ScreenCapture.CaptureScreenshot("graphify-out/car-gameplay-" + id + ".png");
                yield return null;
                Assert.That(Installer.Session.Pause().Succeeded, Is.True);
                var paused = body.position;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(Vector3.Distance(paused, body.position), Is.LessThan(0.01f));
                Assert.That(Installer.Session.Resume().Succeeded, Is.True);
                var resetter = body.GetComponent<VehicleResetter>();
                var beforeReset = resetter.ResetVersion;
                resetter.ResetVehicle();
                Assert.That(resetter.ResetVersion, Is.EqualTo(beforeReset + 1));
            }
        }

        private sealed class CircuitCarTestInput : IDrivingInputProvider
        {
            public DrivingInput Value = new DrivingInput(1, 0, 0, false, false);
            public DrivingInput CurrentInput => Value;
            public void ConsumeResetRequest() { }
            public void ConsumeRecoveryRequest() { }
        }
    }
}
#endif
