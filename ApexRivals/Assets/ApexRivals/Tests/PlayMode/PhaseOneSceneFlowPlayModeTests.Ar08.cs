#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest, Category("AR08")]
        public IEnumerator Ar08RetryFallbackPreservesNonDefaultScaleAndIgnoresDuplicates()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            float duringUnload = -1, duringInstall = -1;
            System.Action<Scene> unloading = scene => { if (scene.name == "Race") duringUnload = Time.timeScale; };
            System.Action<Scene> loaded = scene => { if (scene.name == "Race") duringInstall = Time.timeScale; };
            _loader.ContentSceneUnloading += unloading;
            _loader.ContentSceneLoaded += loaded;
            try
            {
                Time.timeScale = 0.5f;
                var old = Installer.Session;
                var ui = FindInRace<RaceUiSceneInstaller>();
                var view = FindInRace<PauseUguiView>();
                var presenter = (PausePresenter)typeof(PauseUguiView).GetField("_presenter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var retry = old.Retry();
                PressPause(gamepad);
                Assert.That(view.gameObject.activeSelf, Is.True);
                PressPause(gamepad);
                Assert.That(view.gameObject.activeSelf, Is.True, "Duplicate input must remain debounced.");
                Assert.That(old.State, Is.EqualTo(RaceSessionLifecycleState.Retrying));
                ReadReference<Button>(view, "resumeButton").onClick.Invoke();
                Assert.That(Time.timeScale, Is.Zero, "Retry still owns the freeze after fallback closes.");
                Assert.That(view.gameObject.activeSelf, Is.False);
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True);
                Assert.That(old.State, Is.EqualTo(RaceSessionLifecycleState.Disposed));
                Assert.That(_raceInstallCount, Is.EqualTo(2));
                Assert.That(duringUnload, Is.Zero, "The transition remains frozen after the old session disposes.");
                Assert.That(duringInstall, Is.Zero, "The replacement must await SceneFlow completion before time resumes.");
                Assert.That(Installer.Session, Is.Not.SameAs(old));
                Assert.That(Time.timeScale, Is.EqualTo(0.5f));
                Time.timeScale = 0.75f;
                typeof(RaceUiSceneInstaller).GetMethod("CloseFallbackPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                presenter.Resume();
                typeof(PauseUguiView).GetMethod("OnResumeClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                Assert.That(Time.timeScale, Is.EqualTo(0.75f), "Old callbacks must not overwrite the destination's newer decision.");
            }
            finally
            {
                _loader.ContentSceneUnloading -= unloading;
                _loader.ContentSceneLoaded -= loaded;
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [UnityTest, Category("AR08")]
        public IEnumerator Ar08ExitUnloadsOpenFallbackAndReleasesListeners()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Time.timeScale = 0.4f;
                var session = Installer.Session;
                var ui = FindInRace<RaceUiSceneInstaller>();
                var view = FindInRace<PauseUguiView>();
                var exit = session.ReturnToMainMenu();
                PressPause(keyboard);
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                yield return WaitForTask(exit);
                Assert.That(exit.Result.Succeeded, Is.True);
                Assert.That(_flow.State, Is.EqualTo(ApplicationState.MainMenu));
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Disposed));
                Assert.That(StabilityField(ui, "_pauseAction"), Is.Null);
                Assert.That(StabilityField(ui, "_fallbackPauseActive"), Is.EqualTo(false));
                Assert.That(StabilityListeners(view, "ResumeRequested"), Is.Zero);
                Assert.That(StabilityListeners(_flow, "StateChanged"), Is.EqualTo(1), "Only the application-owned pause coordinator remains in MainMenu.");
                Assert.That(Time.timeScale, Is.EqualTo(0.4f));
                var race = _flow.StartRace();
                yield return WaitForTask(race);
                Assert.That(race.Result.Succeeded, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0.4f));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest, Category("AR08")]
        public IEnumerator Ar08FailedRetryRecoversRacing() => Ar08FailedRetryRecovery(RaceSessionLifecycleState.Racing);

        [UnityTest, Category("AR08")]
        public IEnumerator Ar08FailedRetryRecoversPaused() => Ar08FailedRetryRecovery(RaceSessionLifecycleState.Paused);

        [UnityTest, Category("AR08")]
        public IEnumerator Ar08FailedRetryRecoversResults() => Ar08FailedRetryRecovery(RaceSessionLifecycleState.ShowingResults);

        private IEnumerator Ar08FailedRetryRecovery(RaceSessionLifecycleState recovered)
        {
            var gate = new Ar08LoadGate(_loader);
            yield return Ar08UseLoadGate(gate);
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Time.timeScale = 0.6f;
                if (recovered == RaceSessionLifecycleState.Paused) Installer.Session.Pause();
                if (recovered == RaceSessionLifecycleState.ShowingResults) CompleteStabilityRace();
                var session = Installer.Session;
                var player = session.PlayerVehicleRigidbody;
                gate.HoldNext = true;
                var retry = session.Retry();
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Retrying));
                // Results intentionally rejects opening fallback; it still verifies
                // failed results navigation and the prior time-scale decision.
                if (recovered != RaceSessionLifecycleState.ShowingResults)
                {
                    PressPause(gamepad);
                    var view = FindInRace<PauseUguiView>();
                    Assert.That(view.gameObject.activeSelf, Is.True);
                    ReadReference<Button>(view, "resumeButton").onClick.Invoke();
                    Assert.That(Time.timeScale, Is.Zero);
                }
                gate.Release();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.False);
                Assert.That(session.State, Is.EqualTo(recovered));
                Assert.That(session.PlayerVehicleRigidbody, Is.EqualTo(player));
                Assert.That(player.GetComponent<ApexRivals.Input.Runtime.DrivingInputGate>().DrivingAllowed, Is.EqualTo(recovered == RaceSessionLifecycleState.Racing));
                Assert.That(_flow.State, Is.EqualTo(ApplicationState.Race));
                Assert.That(Time.timeScale, Is.EqualTo(recovered == RaceSessionLifecycleState.Paused ? 0f : 0.6f));
                Assert.That(FindInRace<UguiScreenNavigator>().CurrentScreen, Is.EqualTo(recovered == RaceSessionLifecycleState.Paused ? PresentationScreenState.Paused : recovered == RaceSessionLifecycleState.ShowingResults ? PresentationScreenState.Results : PresentationScreenState.Racing));
                if (recovered == RaceSessionLifecycleState.Paused) Assert.That(session.Resume().Succeeded, Is.True);
                var next = session.Retry();
                yield return WaitForTask(next);
                Assert.That(next.Result.Succeeded, Is.True, next.Result.Message);
                Assert.That(Time.timeScale, Is.EqualTo(0.6f));
            }
            finally { gate.Release(); InputSystem.RemoveDevice(gamepad); }
        }

        [UnityTest, Category("AR08")]
        public IEnumerator Ar08SceneFlowOwnsPendingTransitionEvenWithoutSessionRetry()
        {
            var gate = new Ar08LoadGate(_loader);
            yield return Ar08UseLoadGate(gate);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Time.timeScale = 0.4f;
                gate.HoldNext = true;
                var transition = _flow.RetryRace();
                Assert.That(_flow.State, Is.EqualTo(ApplicationState.Transitioning));
                PressPause(keyboard);
                var view = FindInRace<PauseUguiView>();
                Assert.That(view.gameObject.activeSelf, Is.True);
                ReadReference<Button>(view, "resumeButton").onClick.Invoke();
                Assert.That(Time.timeScale, Is.Zero, "SceneFlow still requires the pending replacement to stay frozen.");
                gate.Release();
                yield return WaitForTask(transition);
                Assert.That(transition.Result.Succeeded, Is.False);
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
                Assert.That(Time.timeScale, Is.EqualTo(0.4f));
            }
            finally { gate.Release(); InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest, Category("AR08")]
        public IEnumerator Ar08FallbackDoesNotOverwriteANewerPositiveScale()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                typeof(RaceSessionCoordinator).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Installer.Session, new object[] { RaceSessionLifecycleState.Failed });
                Time.timeScale = 0.4f;
                PressPause(gamepad);
                Assert.That(Time.timeScale, Is.Zero);
                Time.timeScale = 0.7f;
                ReadReference<Button>(FindInRace<PauseUguiView>(), "resumeButton").onClick.Invoke();
                Assert.That(Time.timeScale, Is.EqualTo(0.7f));
            }
            finally { InputSystem.RemoveDevice(gamepad); }
            yield return null;
        }

        private IEnumerator Ar08UseLoadGate(Ar08LoadGate gate)
        {
            var original = _bootstrapper.Context;
            original.PauseCoordinator.Dispose();
            _flow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), gate, new PlayerProfileSaveCheckpoint(original.SaveService));
            var context = new ApplicationContext(_flow, original.ProgressionState, original.SelectedVehicleState, original.SaveService,
                original.VehicleCatalog, raceProgressionService: original.RaceProgressionService);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_bootstrapper, context);
            var menu = _flow.OpenMainMenu();
            yield return WaitForTask(menu);
            Assert.That(menu.Result.Succeeded, Is.True);
            var race = _flow.StartRace();
            yield return WaitForTask(race);
            Assert.That(race.Result.Succeeded, Is.True);
            yield return WaitForStabilityCountdown();
        }

        // Only gates dispatch. Actual loading, scene installation/unloading and the
        // unavailable-scene failure still run through UnityContentSceneLoader.
        private sealed class Ar08LoadGate : IContentSceneLoader
        {
            private readonly IContentSceneLoader _inner;
            private TaskCompletionSource<bool> _release;
            public Ar08LoadGate(IContentSceneLoader inner) { _inner = inner; }
            public bool HoldNext;
            public float Progress => _inner.Progress;
            public bool IsLoading => _release != null || _inner.IsLoading;
            public string BootstrapSceneName => _inner.BootstrapSceneName;
            public string CurrentContentSceneName => _inner.CurrentContentSceneName;
            public async Task<ContentSceneLoadResult> LoadContentSceneAsync(ContentSceneId id, string name, bool reload, CancellationToken cancellationToken = default)
            {
                if (HoldNext)
                {
                    HoldNext = false;
                    _release = new TaskCompletionSource<bool>();
                    await _release.Task;
                    _release = null;
                    name = "Ar08IntentionallyUnavailableScene";
                }
                return await _inner.LoadContentSceneAsync(id, name, reload, cancellationToken);
            }
            public void Release() => _release?.TrySetResult(true);
        }
    }
}
#endif
