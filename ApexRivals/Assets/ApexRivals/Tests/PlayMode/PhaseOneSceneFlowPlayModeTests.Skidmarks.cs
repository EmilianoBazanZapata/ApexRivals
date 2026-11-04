#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Camera.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VFX.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Skidmarks_DriftDrawsOnTheRoadAndBreaksInTheAirAndOnReset()
        {
            MoveSkidmarkRivalsAway();
            var body = Installer.Session.PlayerVehicleRigidbody;
            var tires = body.GetComponent<VehicleSkidmarks>();
            Assert.That(tires, Is.Not.Null);
            for (var i = 0; i < 36; i++)
            {
                body.linearVelocity = body.transform.forward * 14f + body.transform.right * 6f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(tires.SegmentsWritten, Is.GreaterThan(0));
            ShowSkidmarkPreview(body);
            yield return null;
            ScreenCapture.CaptureScreenshot("graphify-out/skidmarks-drift.png");
            yield return null;
            body.position += Vector3.up * 8f + Vector3.forward * 30f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var airborneCount = tires.SegmentsWritten;
            yield return new WaitForSeconds(0.15f);
            Assert.That(tires.SegmentsWritten, Is.EqualTo(airborneCount));
            var resetter = body.GetComponent<VehicleResetter>();
            var version = resetter.ResetVersion;
            resetter.ResetVehicle();
            Assert.That(resetter.ResetVersion, Is.EqualTo(version + 1));
            yield return new WaitForFixedUpdate();
            var mesh = FindInRace<RaceVfxSceneInstaller>().Skidmarks.SharedMesh;
            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i += 4)
                Assert.That(Vector3.Distance(vertices[i], vertices[i + 2]), Is.LessThan(2.6f), "No bridge across a reset or teleport.");
        }

        [UnityTest]
        public IEnumerator Skidmarks_HardBrakingLeavesPersistentMarksAndRetryClearsThem()
        {
            MoveSkidmarkRivalsAway();
            var body = Installer.Session.PlayerVehicleRigidbody;
            var gate = body.GetComponent<ApexRivals.Input.Runtime.DrivingInputGate>();
            gate.Configure(body.gameObject.AddComponent<SkidBrakeTestInput>(), true);
            body.linearVelocity = body.transform.forward * 22f;
            for (var i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            var surface = FindInRace<RaceVfxSceneInstaller>().Skidmarks;
            Assert.That(body.GetComponent<VehicleSkidmarks>().SegmentsWritten, Is.GreaterThan(0));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(22f));
            var count = surface.TotalSegmentsWritten;
            Assert.That(Installer.Session.Pause().Succeeded, Is.True);
            ShowSkidmarkPreview(body);
            // Hide the pause panel only for the screenshot; the pause itself is still active.
            FindInRace<ApexRivals.UI.Runtime.UguiScreenNavigator>().Show(ApexRivals.UI.Runtime.PresentationScreenState.Transitioning);
            yield return null;
            ScreenCapture.CaptureScreenshot("graphify-out/skidmarks-braking.png");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(surface.TotalSegmentsWritten, Is.EqualTo(count));
            var mesh = surface.SharedMesh;
            yield return WaitForTask(Installer.Session.Retry());
            yield return null;
            Assert.That(surface == null, Is.True);
            Assert.That(mesh == null, Is.True, "The runtime mesh is released with its race.");
            Assert.That(FindInRace<RaceVfxSceneInstaller>().Skidmarks.TotalSegmentsWritten, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Skidmarks_ShaderActuallyFadesTheRenderedRubber()
        {
            var configuration = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(
                "Assets/ApexRivals/Content/Configuration/VFX/RaceVfxDefinition.asset"));
            configuration.skidLifetime = 1f;
            configuration.skidMaximumSegments = 128;
            var marks = new GameObject("SkidFadeTest");
            marks.layer = 31;
            var surface = marks.AddComponent<SkidmarkSurface>();
            surface.Initialize(configuration);
            Assert.That(configuration.skidmarkMaterial, Is.Not.Null);
            marks.GetComponent<MeshRenderer>().sharedMaterial = configuration.skidmarkMaterial;
            var cameraObject = new GameObject("SkidFadeCamera", typeof(UnityEngine.Camera));
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            var target = new RenderTexture(64, 64, 16, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0.5f), Quaternion.Euler(90f, 0f, 0f));
            try
            {
                surface.TryAddSegment(Vector3.zero, Vector3.up, Vector3.forward, Vector3.up, 1f);
                yield return null;
                yield return null;
                Assert.That(SampleSkidmarkTexture(target), Is.LessThan(0.85f), "Fresh rubber darkens the rendered surface.");
                yield return new WaitForSeconds(1.2f);
                Assert.That(SampleSkidmarkTexture(target), Is.GreaterThan(0.95f), "Expired rubber leaves the original surface visible.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(marks);
                Object.DestroyImmediate(configuration);
            }
        }

        private static float SampleSkidmarkTexture(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(31, 31, 2, 2), 0, 0);
                image.Apply();
                return image.GetPixel(0, 0).r;
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        [UnityTest]
        public IEnumerator Skidmarks_MeshIsBoundedAndRejectsDisconnectedSurfaceSamples()
        {
            var configuration = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(
                "Assets/ApexRivals/Content/Configuration/VFX/RaceVfxDefinition.asset"));
            configuration.skidMaximumSegments = 128;
            var go = new GameObject("SkidmarkBufferTest");
            var surface = go.AddComponent<SkidmarkSurface>();
            surface.Initialize(configuration);
            try
            {
                Assert.That(surface.TryAddSegment(Vector3.zero, Vector3.up, Vector3.forward * 0.3f, Vector3.up, 1f), Is.True);
                Assert.That(surface.TryAddSegment(Vector3.zero, Vector3.up, Vector3.forward * 100f, Vector3.up, 1f), Is.False);
                Assert.That(surface.TryAddSegment(Vector3.zero, Vector3.up, Vector3.forward * 0.3f, Vector3.down, 1f), Is.False);
                for (var i = 0; i < 200; i++)
                    surface.TryAddSegment(Vector3.forward * (i * 0.3f), Vector3.up, Vector3.forward * ((i + 1) * 0.3f), Vector3.up, 1f);
                yield return null;
                Assert.That(surface.SegmentCount, Is.EqualTo(128));
                Assert.That(surface.SharedMesh.vertexCount, Is.EqualTo(512));
                Assert.That(surface.SharedMesh.GetIndexCount(0), Is.EqualTo(768));
                foreach (var point in surface.SharedMesh.vertices) Assert.That(point.y, Is.EqualTo(0.012f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(configuration); }
        }

        private static void MoveSkidmarkRivalsAway()
        {
            var coordinator = FindInRace<RaceCoordinator>();
            foreach (var racer in coordinator.Participants)
                if (racer != coordinator.PlayerParticipant) racer.GetComponentInParent<Rigidbody>().position += Vector3.right * 50f;
        }

        private static void ShowSkidmarkPreview(Rigidbody body)
        {
            var follow = FindInRace<WheelVehicleFollowCamera>();
            follow.enabled = false;
            var position = body.position - body.transform.forward * 9f + Vector3.up * 8f;
            follow.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(body.position - body.transform.forward * 3f - position, Vector3.up));
        }

        private sealed class SkidBrakeTestInput : MonoBehaviour, IDrivingInputProvider
        {
            public DrivingInput CurrentInput => new DrivingInput(0f, 1f, 0f, false, false);
            public void ConsumeResetRequest() { }
            public void ConsumeRecoveryRequest() { }
        }
    }
}
#endif
