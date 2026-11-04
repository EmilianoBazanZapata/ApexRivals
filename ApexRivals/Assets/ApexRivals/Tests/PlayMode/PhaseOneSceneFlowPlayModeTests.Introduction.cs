#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Composition.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.UI.Runtime;
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
        public IEnumerator Introduction_AllCircuitsHoldGridThenHandOffBeforeCountdown()
        {
            _playIntroduction = true;
            _installAllContentScenes = true;
            var context = _bootstrapper.Context;
            var tracks = new[] { "Race", "RaceDunes", "RaceSuzuka" };
            foreach (var track in tracks)
            {
                context.SelectedVehicleState.Commit(track == "RaceDunes" ? "striker" : "vanguard");
                _flow.SelectRaceScene(track);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                var installer = FindInScene<RaceSessionSceneInstaller>(track);
                var intro = (PreRacePresentation)installer.Introduction;
                var coordinator = FindInScene<RaceCoordinator>(track);
                var follow = FindInScene<WheelVehicleFollowCamera>(track);
                var session = installer.Session;
                Assert.That(intro.IsPresenting, Is.True);
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
                Assert.That(session.Pause().Succeeded, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                var body = session.PlayerVehicleRigidbody;
                var start = body.position;
                var captured = new bool[4];
                var shotBegan = new float[4];
                var deadline = Time.realtimeSinceStartup + 9f;
                while (intro.IsPresenting && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
                    Assert.That(coordinator.ElapsedRaceTime, Is.Zero);
                    Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
                    Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.03f));
                    foreach (var racer in coordinator.Participants)
                        Assert.That(racer.GetComponentInParent<ApexRivals.Input.Runtime.DrivingInputGate>().DrivingAllowed, Is.False);
                    if (intro.ShotIndex >= 0)
                    {
                        Assert.That(follow.enabled, Is.False, "Cinemachine exclusively owns the output during the intro.");
                        if (shotBegan[intro.ShotIndex] == 0f) shotBegan[intro.ShotIndex] = Time.realtimeSinceStartup;
                        if (!captured[intro.ShotIndex] && Time.realtimeSinceStartup - shotBegan[intro.ShotIndex] > 0.8f)
                        {
                            captured[intro.ShotIndex] = true;
                            // Review artifacts only; profiles and authored assets are untouched.
                            ScreenCapture.CaptureScreenshot("graphify-out/intro-" + track + "-" + intro.ShotIndex + ".png");
                        }
                    }
                    yield return null;
                }
                Assert.That(intro.IsPresenting, Is.False, "Introduction timed out.");
                Assert.That(captured, Is.All.True);
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
                Assert.That(follow.enabled, Is.True);
                Assert.That(body.constraints, Is.Not.EqualTo(RigidbodyConstraints.FreezeAll));
                var cameraPosition = follow.transform.position;
                yield return null;
                Assert.That(Vector3.Distance(cameraPosition, follow.transform.position), Is.LessThan(0.2f), "No handoff snap.");
                Assert.That(coordinator.ElapsedRaceTime, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator Introduction_SkipIsIdempotentAndPauseInputDoesNotOpenFallback()
        {
            _playIntroduction = true;
            yield return WaitForTask(_flow.RetryRace());
            var intro = (PreRacePresentation)Installer.Introduction;
            var session = Installer.Session;
            var deadline = Time.realtimeSinceStartup + 3f;
            while (intro.ShotIndex < 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(intro.ShotIndex, Is.EqualTo(0));
            var started = 0;
            session.StateChanged += (previous, current) => { if (current == RaceSessionLifecycleState.Starting) started++; };
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
                InputSystem.Update();
                yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.Update();
                yield return null;
                yield return null;
                intro.RequestSkip();
                intro.RequestSkip();
                Assert.That(intro.IsPresenting, Is.False);
                Assert.That(started, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
                Assert.That(FindInRace<WheelVehicleFollowCamera>().enabled, Is.True);
            }
            finally { InputSystem.RemoveDevice(gamepad); }
        }

        [UnityTest]
        public IEnumerator Introduction_WaitsForLoadingDismissalThenAcceptsKeyboardSkip()
        {
            var loading = new HeldIntroductionLoading();
            var original = _bootstrapper.Context;
            var context = new ApplicationContext(_flow, original.ProgressionState, original.SelectedVehicleState,
                original.SaveService, original.VehicleCatalog, raceProgressionService: original.RaceProgressionService,
                loadingPresentation: loading);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_bootstrapper, context);
            _playIntroduction = true;
            yield return WaitForTask(_flow.RetryRace());
            var intro = (PreRacePresentation)Installer.Introduction;
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(intro.IsPresenting, Is.True);
            Assert.That(intro.ShotIndex, Is.EqualTo(-1), "No shots run behind the loading screen.");
            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
            loading.Visible = false;
            var deadline = Time.realtimeSinceStartup + 2f;
            while (intro.ShotIndex < 0 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; // Arm only after the selection-confirmation key has been released.
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(intro.IsPresenting, Is.False);
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        private sealed class HeldIntroductionLoading : ISceneLoadingPresentation
        {
            public bool Visible = true;
            public bool IsVisible => Visible;
            public System.Threading.Tasks.Task ShowAsync() => System.Threading.Tasks.Task.CompletedTask;
            public void Hide(bool succeeded) => Visible = false;
        }

        [UnityTest]
        public IEnumerator Introduction_UnloadCancelsWithoutStartingTheDisposedAttempt()
        {
            _playIntroduction = true;
            yield return WaitForTask(_flow.RetryRace());
            var oldSession = Installer.Session;
            var started = 0;
            oldSession.StateChanged += (previous, current) => { if (current == RaceSessionLifecycleState.Starting) started++; };
            yield return WaitForTask(_flow.ForceReturnToMainMenu());
            yield return null;
            Assert.That(oldSession.State, Is.EqualTo(RaceSessionLifecycleState.Disposed));
            Assert.That(started, Is.Zero);
            Assert.That(_loader.CurrentContentSceneName, Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator Introduction_MissingCameraFallsBackToCountdown()
        {
            _playIntroduction = true;
            yield return WaitForTask(_flow.RetryRace());
            var intro = (PreRacePresentation)Installer.Introduction;
            typeof(PreRacePresentation).GetField("followCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(intro, null);
            LogAssert.Expect(LogType.Warning, "Race introduction has no camera or player target; starting the normal countdown.");
            yield return null;
            yield return null;
            Assert.That(intro.IsPresenting, Is.False);
            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
            Assert.That(Installer.Session.PlayerVehicleRigidbody.constraints, Is.Not.EqualTo(RigidbodyConstraints.FreezeAll));
        }
    }
}
#endif
