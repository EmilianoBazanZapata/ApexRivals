#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator CarPresentation_AllCircuitsLoopUntilConfirmedAndHandOffOnce()
        {
            _playIntroduction = _installAllContentScenes = _confirmCarPresentation = true;
            foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
            {
                _bootstrapper.Context.SelectedVehicleState.Commit(track == "RaceDunes" ? "striker" : "vanguard");
                _flow.SelectRaceScene(track);
                yield return WaitForTask(_flow.RetryRace());
                var installer = FindInScene<RaceSessionSceneInstaller>(track);
                var intro = installer.Introduction;
                var session = installer.Session;
                var body = session.PlayerVehicleRigidbody;
                var start = body.position;
                var seen = new bool[3];
                var deadline = Time.realtimeSinceStartup + 8f;
                var coordinator = FindInScene<RaceCoordinator>(track);
                while (intro.CompletedPresentationLoops < 2 && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(intro.IsPresenting, Is.True);
                    Assert.That(intro.AwaitingStart, Is.True);
                    Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
                    Assert.That(coordinator.ElapsedRaceTime, Is.Zero);
                    Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
                    Assert.That(Vector3.Distance(body.position, start), Is.LessThan(.03f));
                    if (intro.ShotIndex >= 0 && intro.ShotIndex < 3) seen[intro.ShotIndex] = true;
                    if (intro.ShotIndex >= 0) Assert.That(FindInScene<WheelVehicleFollowCamera>(track).enabled, Is.False);
                    yield return null;
                }
                Assert.That(intro.CompletedPresentationLoops, Is.GreaterThanOrEqualTo(2));
                Assert.That(seen, Is.All.True);
                var button = new SerializedObject(intro).FindProperty("startRaceButton").objectReferenceValue as Button;
                Assert.That(button, Is.Not.Null);
                Assert.That(button.interactable, Is.True);
                var starts = 0;
                session.StateChanged += (_, current) => { if (current == RaceSessionLifecycleState.Starting) starts++; };
                button.onClick.Invoke();
                button.onClick.Invoke();
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Ready), "Confirmation must hand over the camera before starting the countdown.");
                deadline = Time.realtimeSinceStartup + 3f;
                while (intro.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(intro.IsPresenting, Is.False);
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
                Assert.That(body.constraints, Is.Not.EqualTo(RigidbodyConstraints.FreezeAll));
                Assert.That(FindInScene<WheelVehicleFollowCamera>(track).enabled, Is.True);
                var position = FindInScene<WheelVehicleFollowCamera>(track).transform.position;
                yield return null;
                Assert.That(Vector3.Distance(position, FindInScene<WheelVehicleFollowCamera>(track).transform.position), Is.LessThan(.2f));
            }
        }

        [UnityTest]
        public IEnumerator CarPresentation_HeldKeyboardConfirmationWaitsForReleaseAndNewPress()
        {
            _playIntroduction = _confirmCarPresentation = true;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                InputSystem.Update();
                yield return WaitForTask(_flow.RetryRace());
                var intro = Installer.Introduction;
                var deadline = Time.realtimeSinceStartup + 3f;
                while (intro.ShotIndex < 0 && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSeconds(.3f);
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                deadline = Time.realtimeSinceStartup + 3f;
                while (intro.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(intro.IsPresenting, Is.False);
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator CarPresentation_GamepadConfirmsPauseIsBlockedAndUnloadReleasesGridAndScope()
        {
            _playIntroduction = _confirmCarPresentation = true;
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                yield return WaitForTask(_flow.RetryRace());
                var intro = Installer.Introduction;
                var deadline = Time.realtimeSinceStartup + 3f;
                while (intro.ShotIndex < 0 && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
                InputSystem.Update();
                yield return null;
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update();
                deadline = Time.realtimeSinceStartup + 3f;
                while (intro.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(intro.IsPresenting, Is.False);
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
                yield return WaitForTask(_flow.RetryRace());
                intro = Installer.Introduction;
                deadline = Time.realtimeSinceStartup + 3f;
                while (intro.ShotIndex < 0 && Time.realtimeSinceStartup < deadline) yield return null;
                var body = Installer.Session.PlayerVehicleRigidbody;
                intro.Cancel();
                Assert.That(body.constraints, Is.Not.EqualTo(RigidbodyConstraints.FreezeAll));
                Assert.That(ApexRivals.UI.Runtime.UiFocus.HasActiveScope, Is.False);
                yield return WaitForTask(_flow.ForceReturnToMainMenu());
                Assert.That(intro == null, Is.True);
            }
            finally { InputSystem.RemoveDevice(gamepad); }
        }
    }
}
#endif
