using System;
using System.Collections;
using System.Reflection;
using ApexRivals.Camera.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class VehiclePresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator FollowCamera_UsesPhysicalReverseSpeedWithHysteresis()
        {
            var vehicle = new GameObject("Vehicle");
            var vehicleRigidbody = vehicle.AddComponent<Rigidbody>();
            var cameraObject = new GameObject("FollowCamera");
            cameraObject.AddComponent<UnityEngine.Camera>();
            var followCamera = cameraObject.AddComponent<WheelVehicleFollowCamera>();
            followCamera.SetTarget(vehicle.transform);
            followCamera.SetVehicleRigidbody(vehicleRigidbody);

            vehicleRigidbody.linearVelocity = Vector3.back;
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();

            Assert.That(followCamera.CurrentSignedForwardSpeed, Is.EqualTo(-1f).Within(0.01f));
            Assert.That(followCamera.ReverseCameraActive, Is.True);
            Assert.That(Vector3.Dot(
                followCamera.CurrentDesiredCameraPosition - vehicle.transform.position,
                vehicle.transform.forward), Is.GreaterThan(0f));

            vehicleRigidbody.linearVelocity = Vector3.back * 0.5f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();

            Assert.That(followCamera.ReverseCameraActive, Is.True);

            vehicleRigidbody.linearVelocity = Vector3.back * 0.1f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();

            Assert.That(followCamera.ReverseCameraActive, Is.False);

            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(vehicle);
        }

        [UnityTest]
        public IEnumerator GaragePreview_NeutralizesTheInstantiatedVehicleOnly()
        {
            var previewAnchor = new GameObject("PreviewAnchor");
            var sourceVehicle = new GameObject("VehicleSource");
            sourceVehicle.AddComponent<Rigidbody>();
            var sourceMarker = sourceVehicle.AddComponent<PreviewRuntimeMarker>();
            var controllerObject = new GameObject("PreviewController");
            var controller = controllerObject.AddComponent<GarageVehiclePreviewController>();
            SetPrivateField(controller, "previewAnchor", previewAnchor.transform);
            ConfigurePreviewEntry(controller, "vanguard", sourceVehicle);

            controller.ShowVehicle("vanguard");
            yield return null;

            Assert.That(previewAnchor.transform.childCount, Is.EqualTo(1));
            var previewRigidbody = previewAnchor.GetComponentInChildren<Rigidbody>(true);
            var previewMarker = previewAnchor.GetComponentInChildren<PreviewRuntimeMarker>(true);
            Assert.That(previewRigidbody.isKinematic, Is.True);
            Assert.That(previewRigidbody.useGravity, Is.False);
            Assert.That(previewMarker.enabled, Is.False);
            Assert.That(sourceMarker.enabled, Is.True);

            controller.ShowVehicle("unknown");
            yield return null;

            Assert.That(previewAnchor.transform.childCount, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(sourceVehicle);
            UnityEngine.Object.DestroyImmediate(previewAnchor);
        }

        private static void ConfigurePreviewEntry(GarageVehiclePreviewController controller, string vehicleId, GameObject prefab)
        {
            var entriesField = typeof(GarageVehiclePreviewController).GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entryType = entriesField.FieldType.GetElementType();
            var entries = Array.CreateInstance(entryType, 1);
            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("vehicleId").SetValue(entry, vehicleId);
            entryType.GetField("prefab").SetValue(entry, prefab);
            entries.SetValue(entry, 0);
            entriesField.SetValue(controller, entries);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private sealed class PreviewRuntimeMarker : MonoBehaviour
        {
        }
    }
}
