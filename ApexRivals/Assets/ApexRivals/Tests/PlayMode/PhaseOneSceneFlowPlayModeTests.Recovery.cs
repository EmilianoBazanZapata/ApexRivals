#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Recovery_UprightOutsideTrackAndAirborneAcceptEAndGamepadView()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            _installAllContentScenes = true;
            try
            {
                foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
                {
                    _flow.SelectRaceScene(track);
                    yield return WaitForTask(_flow.RetryRace());
                    var session = FindInScene<RaceSessionSceneInstaller>(track).Session;
                    var deadline = Time.realtimeSinceStartup + 8;
                    while (session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
                    var coordinator = FindInScene<RaceCoordinator>(track);
                    var body = session.PlayerVehicleRigidbody;
                    var participant = body.GetComponent<RaceParticipant>();
                    var detector = body.GetComponent<VehicleRoofRecoveryDetector>();
                    var resetter = body.GetComponent<VehicleResetter>();
                    yield return new WaitForSeconds(.75f);
                    Assert.That(detector.CanRecoverVehicle, Is.False, track + " starting grid is part of the road.");
                    Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                    var target = Mathf.FloorToInt(coordinator.OrderedCheckpoints.Count * .7f);
                    while (progress.NextExpectedCheckpointIndex <= target)
                    {
                        var cp = coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex];
                        body.position = cp.Position + Vector3.up * 10;
                        Assert.That(coordinator.TryPassCheckpoint(participant, cp).Accepted, Is.True);
                    }
                    var last = coordinator.OrderedCheckpoints[target];
                    Assert.That(coordinator.TryGetRecoveryPose(participant, last.Position + last.transform.forward * 15,
                        out var road, out var rotation, out _), Is.True);
                    body.position = road + Vector3.up * .35f;
                    body.rotation = rotation;
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    yield return new WaitForSeconds(1);
                    var revision = resetter.ResetVersion;
                    Assert.That(detector.CanRecoverVehicle, Is.False);
                    PressRecovery(keyboard);
                    yield return null;
                    yield return null;
                    Assert.That(resetter.ResetVersion, Is.EqualTo(revision), "E on the road must not teleport the car.");
                    var next = progress.NextExpectedCheckpointIndex;
                    var passed = progress.TotalPassedCheckpoints;
                    var laps = progress.CompletedLaps;
                    var constraints = body.constraints;
                    body.constraints = RigidbodyConstraints.FreezeAll;
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        body.position = attempt == 2 ? road + Vector3.up * 20 : road + rotation * Vector3.right * 35 + Vector3.up * 2;
                        if (attempt != 2)
                        {
                            // Avoid another nearby section of a tightly folded circuit.
                            for (var offset = 35; offset <= 100; offset += 5)
                            {
                                var candidate = road + rotation * Vector3.right * offset + Vector3.up * 2;
                                if (!coordinator.TryGetNearestTrackPoint(participant, candidate, out var nearest)) continue;
                                if (Vector3.ProjectOnPlane(candidate - nearest, Vector3.up).magnitude <= 12) continue;
                                body.position = candidate;
                                break;
                            }
                        }
                        body.rotation = rotation; // Upright in every case, including the airborne launch.
                        yield return new WaitForSeconds(.75f);
                        Assert.That(detector.IsInverted, Is.False);
                        Assert.That(detector.IsOutsideTrack, Is.True, track);
                        Assert.That(detector.CanRecoverVehicle, Is.True, track);
                        revision = resetter.ResetVersion;
                        PressRecovery(attempt == 1 ? (InputDevice)gamepad : keyboard);
                        yield return null;
                        yield return null;
                        Assert.That(resetter.ResetVersion, Is.EqualTo(revision + 1), track + " must recover exactly once.");
                        Assert.That(detector.LastRecoveryInput, Is.EqualTo(attempt == 1 ? RecoveryInputSource.GamepadView : RecoveryInputSource.KeyboardE));
                        Assert.That(Vector3.Distance(body.position, road), Is.LessThan(5));
                        Assert.That(detector.CanRecoverVehicle, Is.False);
                        Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(next));
                        Assert.That(progress.TotalPassedCheckpoints, Is.EqualTo(passed));
                        Assert.That(progress.CompletedLaps, Is.EqualTo(laps));
                    }
                    body.constraints = constraints;
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
            }
        }

        private static void PressRecovery(InputDevice device)
        {
            if (device is Keyboard keyboard)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            else if (device is Gamepad gamepad)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Select));
                InputSystem.Update();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
            }
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator Recovery_SeventyPercentNeverReturnsToStartAndKeepsTrackingTheRoad()
        {
            _installAllContentScenes = true;
            foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
            {
                _flow.SelectRaceScene(track);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                var session = FindInScene<RaceSessionSceneInstaller>(track).Session;
                var deadline = Time.realtimeSinceStartup + 8;
                while (session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
                var coordinator = FindInScene<RaceCoordinator>(track);
                Assert.That(coordinator.HasRecoveryRoute, Is.True, track);
                var body = session.PlayerVehicleRigidbody;
                var participant = body.GetComponent<RaceParticipant>();
                var controller = body.GetComponent<WheelArcadeVehicleController>();
                Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                var targetIndex = Mathf.FloorToInt(coordinator.OrderedCheckpoints.Count * .7f);
                var start = body.position;
                // Reproduce valid progress without letting any airborne crossing capture
                // a grounded anchor. The old system retains its starting-grid position.
                while (progress.NextExpectedCheckpointIndex <= targetIndex)
                {
                    var checkpoint = coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex];
                    body.position = checkpoint.Position + Vector3.up * 10;
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    Assert.That(coordinator.TryPassCheckpoint(participant, checkpoint).Accepted, Is.True);
                }
                var last = coordinator.OrderedCheckpoints[targetIndex];
                var next = progress.NextExpectedCheckpointIndex;
                var passes = progress.TotalPassedCheckpoints;
                var laps = progress.CompletedLaps;
                body.position += Vector3.right * 25;
                body.rotation = Quaternion.Euler(0,0,180);
                Assert.That(controller.TryResetVehicle(), Is.True, track + " must resolve the last accepted road checkpoint even after an airborne crossing.");
                Assert.That(Vector3.Distance(body.position, last.Position), Is.LessThan(5), track);
                Assert.That(Vector3.Distance(body.position, start), Is.GreaterThan(30), track);
                Assert.That(Vector3.Dot(body.transform.up, Vector3.up), Is.GreaterThan(.99f));

                // A safe anchor also advances between checkpoints, rather than sending
                // the player all the way back through a long sector after a rollover.
                Assert.That(coordinator.TryGetRecoveryPose(participant, last.Position + last.transform.forward * 15,
                    out var roadPoint, out var rotation, out _), Is.True);
                body.position = roadPoint + Vector3.up * .35f;
                body.rotation = rotation;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                yield return new WaitForSeconds(1);
                var settled = body.position;
                Assert.That(Vector3.Distance(settled, last.Position), Is.GreaterThan(8));
                body.position += Vector3.right * 25 + Vector3.up * 3;
                body.rotation = Quaternion.Euler(0,0,180);
                Assert.That(controller.TryResetVehicle(), Is.True, track);
                Assert.That(Vector3.Distance(body.position, roadPoint), Is.LessThan(5), track + " must restore near the latest safe progress.");
                Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(next));
                Assert.That(progress.TotalPassedCheckpoints, Is.EqualTo(passes));
                Assert.That(progress.CompletedLaps, Is.EqualTo(laps));
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            }
        }
    }
}
#endif
