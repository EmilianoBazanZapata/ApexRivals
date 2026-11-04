#if UNITY_EDITOR
using System.Collections;
using ApexRivals.UI.Runtime;
using ApexRivals.AI.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Composition.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.DrivingAssistance.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator DrivingLine_AllCircuitsChangeColorWithPlayerSpeedAndKeepGeometryOnRoad()
        {
            _installAllContentScenes = true;
            foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
            {
                _bootstrapper.Context.SelectedVehicleState.Commit(track == "RaceDunes" ? "striker" : "vanguard");
                _flow.SelectRaceScene(track);
                yield return WaitForTask(_flow.RetryRace());
                var line = FindInScene<DynamicDrivingLine>(track);
                var installer = FindInScene<RaceSessionSceneInstaller>(track);
                var body = installer.Session.PlayerVehicleRigidbody;
                PrepareLineTestBody(body);
                Assert.That(line.MarkerCount, Is.InRange(100, 4096));
                var mesh = line.Mesh;
                var originalVertices = mesh.vertices;
                var sharpest = 0;
                for (var i = 1; i < line.Path.SampleCount; i++)
                    if (line.Path.GetSample(i).Curvature > line.Path.GetSample(sharpest).Curvature) sharpest = i;
                var sample = line.Path.GetSample(sharpest);
                yield return HoldLineTestSpeed(body, sample, 0);
                Assert.That(line.IsVisible, Is.True);
                Assert.That(line.CurrentZone, Is.EqualTo(DrivingLineZone.Green));
                Assert.That(line.VisibleMarkerCount, Is.InRange(10, 80));
                var recommended = line.CurrentRecommendedSpeed;
                Assert.That(recommended, Is.GreaterThan(0));
                FindInScene<WheelVehicleFollowCamera>(track).SnapToTarget();
                ScreenCapture.CaptureScreenshot("graphify-out/driving-line-" + track + "-green.png");
                yield return null;
                yield return HoldLineTestSpeed(body, sample, recommended * 0.95f);
                Assert.That(line.CurrentZone, Is.EqualTo(DrivingLineZone.Yellow));
                ScreenCapture.CaptureScreenshot("graphify-out/driving-line-" + track + "-yellow.png");
                yield return null;
                yield return HoldLineTestSpeed(body, sample, recommended * 1.4f);
                Assert.That(line.CurrentZone, Is.EqualTo(DrivingLineZone.Red));
                ScreenCapture.CaptureScreenshot("graphify-out/driving-line-" + track + "-red.png");
                yield return null;
                Assert.That(line.Mesh, Is.SameAs(mesh));
                Assert.That(mesh.vertices, Is.EqualTo(originalVertices), "Speed changes only colors, not road geometry.");
                Assert.That(line.GetComponentsInChildren<Collider>(), Is.Empty);
                var road = GameObject.Find("RaceSceneRoot").transform.Find("Track/Circuit/Road").GetComponent<MeshCollider>();
                var colors = mesh.colors32;
                for (var i = 0; i < colors.Length; i++)
                {
                    if (colors[i].a == 0) continue;
                    var vertex = line.transform.TransformPoint(originalVertices[i]);
                    Assert.That(road.Raycast(new Ray(vertex + Vector3.up * 0.2f, Vector3.down), out var hit, 0.4f), Is.True);
                    Assert.That(Vector3.Distance(vertex, hit.point), Is.InRange(0.025f, 0.08f));
                }
            }
        }

        [UnityTest]
        public IEnumerator DrivingLine_SuzukaResetReanchorsBetweenBridgeAndLowerRoute()
        {
            _installAllContentScenes = true;
            _flow.SelectRaceScene("RaceSuzuka");
            yield return WaitForTask(_flow.RetryRace());
            var line = FindInScene<DynamicDrivingLine>("RaceSuzuka");
            var body = FindInScene<RaceSessionSceneInstaller>("RaceSuzuka").Session.PlayerVehicleRigidbody;
            PrepareLineTestBody(body);
            var high = 0;
            for (var i = 1; i < line.MarkerCount; i++)
                if (line.Path.GetSample(i).Position.y > line.Path.GetSample(high).Position.y) high = i;
            var upper = line.Path.GetSample(high);
            Assert.That(upper.Position.y, Is.GreaterThan(3));
            yield return HoldLineTestSpeed(body, upper, 0);
            Assert.That(line.IsVisible, Is.True);
            AssertLineAnchor(line, upper);
            var low = -1;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < line.MarkerCount; i++)
            {
                var candidate = line.Path.GetSample(i);
                if (candidate.Position.y > upper.Position.y - 3) continue;
                var distance = Vector3.ProjectOnPlane(candidate.Position - upper.Position, Vector3.up).sqrMagnitude;
                if (distance < nearest) { nearest = distance; low = i; }
            }
            Assert.That(low, Is.GreaterThanOrEqualTo(0));
            var lower = line.Path.GetSample(low);
            var resetter = body.GetComponent<VehicleResetter>();
            resetter.CaptureResetPose(lower.Position + Vector3.up * 1.5f, Quaternion.LookRotation(lower.Forward));
            resetter.ResetVehicle();
            yield return new WaitForSeconds(0.2f);
            Assert.That(line.IsVisible, Is.True);
            AssertLineAnchor(line, lower);
        }

        [UnityTest]
        public IEnumerator DrivingLine_IntroductionPauseResultsToggleAndRetryRespectVisibilityAndOwnership()
        {
            var line = FindInRace<DynamicDrivingLine>();
            Assert.That(line.IsVisible, Is.True);
            line.AssistanceEnabled = false;
            yield return null;
            Assert.That(line.IsVisible, Is.False);
            line.AssistanceEnabled = true;
            yield return new WaitForSeconds(0.15f);
            Assert.That(line.IsVisible, Is.True);
            Installer.Session.Pause();
            yield return null;
            Assert.That(line.IsVisible, Is.False);
            Installer.Session.Resume();
            yield return new WaitForSeconds(0.15f);
            Assert.That(line.IsVisible, Is.True);
            CompleteStabilityRace();
            yield return null;
            Assert.That(line.IsVisible, Is.False);
            var oldMesh = line.Mesh;
            _playIntroduction = true;
            yield return WaitForTask(Installer.Session.Retry());
            yield return null;
            Assert.That(line == null, Is.True);
            Assert.That(oldMesh == null, Is.True, "Runtime mesh must be released on unload.");
            var replacement = FindInRace<DynamicDrivingLine>();
            Assert.That(replacement.IsVisible, Is.False);
            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
            var deadline = Time.realtimeSinceStartup + 4;
            while (((PreRacePresentation)Installer.Introduction).ShotIndex < 0 && Time.realtimeSinceStartup < deadline) yield return null;
            ((PreRacePresentation)Installer.Introduction).RequestSkip();
            deadline = Time.realtimeSinceStartup + 3;
            while (Installer.Session.State == RaceSessionLifecycleState.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(0.15f);
            Assert.That(replacement.IsVisible, Is.True);
        }

        private static void PrepareLineTestBody(Rigidbody body)
        {
            body.GetComponent<WheelArcadeVehicleController>().enabled = false;
            foreach (var wheel in body.GetComponentsInChildren<WheelCollider>()) wheel.enabled = false;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        private static IEnumerator HoldLineTestSpeed(Rigidbody body, IdealRacingPath.Sample sample, float speed)
        {
            var deadline = Time.time + 0.22f;
            do
            {
                body.position = sample.Position + Vector3.up * 1.5f;
                body.rotation = Quaternion.LookRotation(sample.Forward);
                body.linearVelocity = sample.Forward * speed;
                yield return null;
            } while (Time.time < deadline);
        }
        private static void AssertLineAnchor(DynamicDrivingLine line, IdealRacingPath.Sample sample)
        {
            var delta = Mathf.Abs(line.DistanceAlongLap - sample.DistanceAlongLap);
            Assert.That(Mathf.Min(delta, line.Path.LapLength - delta), Is.LessThan(10), "Track progress must remain on the intended level and route section.");
        }
    }
}
#endif
