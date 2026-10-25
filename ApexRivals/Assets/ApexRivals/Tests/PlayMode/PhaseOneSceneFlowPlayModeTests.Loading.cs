#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEditor;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator LoadingOverlaySurvivesRealSceneSwapThroughInstallationAndRollback()
        {
            var overlay = _loader.gameObject.AddComponent<SceneLoadingOverlay>();
            overlay.PreLoadDelaySeconds = 0f; // The dedicated overlay test verifies the real five-second delay.
            var input = _loader.gameObject.AddComponent<BootstrapUiInputSystem>();
            using (var serialized = new SerializedObject(input))
            {
                serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            input.SendMessage("ConfigureInputModule");
            overlay.Bind(input);
            var presented = new LoadingContentSceneLoader(_loader, overlay, Debug.LogException);
            var original = _bootstrapper.Context;
            original.PauseCoordinator.Dispose();
            _flow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), presented,
                new PlayerProfileSaveCheckpoint(original.SaveService));
            var selection = new ApexRivals.VehicleSelection.Runtime.VehicleSelectionService(original.VehicleCatalog,
                original.SelectedVehicleState, new PlayerProfileSaveCheckpoint(original.SaveService), original.ProgressionState);
            var context = new ApplicationContext(_flow, original.ProgressionState, original.SelectedVehicleState,
                original.SaveService, original.VehicleCatalog, selection,
                settingsService: new ApexRivals.Settings.Runtime.SettingsService(new MemorySettingsStorage(), new MemoryDisplaySettings()),
                raceProgressionService: original.RaceProgressionService,
                loadingPresentation: overlay);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_bootstrapper, context);
            _installAllContentScenes = true;
            var observedInstallation = false;
            var installationWasCovered = true;
            System.Action<Scene> installed = scene =>
            {
                installationWasCovered &= overlay != null && overlay.IsVisible;
                observedInstallation = true;
            };
            _loader.ContentSceneLoaded += installed;
            try
            {
                // The fixture already started a race with isolated in-memory storage.
                var menu = _flow.OpenMainMenu();
                Assert.That(overlay.IsVisible, Is.True);
                yield return WaitForTask(menu);
                Assert.That(menu.Result.Succeeded, Is.True, menu.Result.Message);
                Assert.That(observedInstallation, Is.True);
                Assert.That(overlay.IsVisible, Is.False);
                Assert.That(overlay.gameObject.scene.name, Is.EqualTo("PhaseOneTestBootstrap"));

                observedInstallation = false;
                var race = _flow.StartRace();
                yield return WaitForTask(race);
                Assert.That(race.Result.Succeeded, Is.True, race.Result.Message);
                Assert.That(observedInstallation, Is.True);
                Assert.That(installationWasCovered, Is.True);
                Assert.That(Installer.LastInstallResult.Succeeded, Is.True);
                Assert.That(overlay.IsVisible, Is.False);
                Assert.That(_loader.GetComponents<SceneLoadingOverlay>().Length, Is.EqualTo(1));
                Assert.That(Installer.Session.State, Is.EqualTo(ApexRivals.RaceSession.Runtime.RaceSessionLifecycleState.Starting));

                yield return WaitForStabilityCountdown();
                Assert.That(Installer.Session.Pause().Succeeded, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                var pausedExit = Installer.Session.ReturnToMainMenu();
                Assert.That(overlay.IsVisible, Is.True);
                var raceUi = FindInRace<RaceUiSceneInstaller>();
                var gamepad = InputSystem.AddDevice<Gamepad>();
                try { PressPause(gamepad); }
                finally { InputSystem.RemoveDevice(gamepad); }
                Assert.That(typeof(RaceUiSceneInstaller).GetField("_fallbackPauseActive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(raceUi), Is.False);
                yield return WaitForTask(pausedExit);
                Assert.That(overlay.IsVisible, Is.False);
                Assert.That(_flow.State, Is.EqualTo(ApplicationState.MainMenu));
                Assert.That(input.IsLoadingInputBlocked, Is.False);
                yield return null;
                Assert.That(UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)), Is.True);
                race = _flow.StartRace();
                yield return WaitForTask(race);

                var missing = presented.LoadContentSceneAsync(ContentSceneId.MainMenu, "UnavailableLoadingTestScene", false);
                yield return WaitForTask(missing);
                Assert.That(missing.Result.Succeeded, Is.False);
                Assert.That(overlay.IsVisible, Is.False);
                Assert.That(_loader.CurrentContentSceneName, Is.EqualTo("Race"));

                using (var cancellation = new System.Threading.CancellationTokenSource())
                {
                    // Cancel after Unity activates the candidate, exercising the real rollback.
                    UnityEngine.Events.UnityAction<Scene, LoadSceneMode> cancelCandidate = (scene, mode) =>
                    {
                        if (scene.name == "MainMenu") cancellation.Cancel();
                    };
                    SceneManager.sceneLoaded += cancelCandidate;
                    try
                    {
                        var cancelled = presented.LoadContentSceneAsync(ContentSceneId.MainMenu, "MainMenu", false, cancellation.Token);
                        yield return WaitForTask(cancelled);
                        Assert.That(cancelled.Result.Succeeded, Is.False);
                        Assert.That(overlay.IsVisible, Is.False);
                        Assert.That(_loader.CurrentContentSceneName, Is.EqualTo("Race"));
                        Assert.That(SceneManager.GetSceneByName("MainMenu").isLoaded, Is.False);
                    }
                    finally { SceneManager.sceneLoaded -= cancelCandidate; }
                }

                yield return WaitForStabilityCountdown();
                var coordinator = FindInRace<ApexRivals.Race.Runtime.RaceCoordinator>();
                var participant = coordinator.PlayerParticipant;
                for (var index = 0; index < coordinator.TotalLaps * coordinator.OrderedCheckpoints.Count + 1; index++)
                {
                    Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                    if (progress.HasFinished) break;
                    Assert.That(coordinator.TryPassCheckpoint(participant, coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex]).Accepted, Is.True);
                }
                yield return null;
                Assert.That(FindInRace<UguiScreenNavigator>().CurrentScreen, Is.EqualTo(PresentationScreenState.Results));
                ReadReference<Button>(FindInRace<ResultsUguiView>(), "returnToMainMenuButton").onClick.Invoke();
                Assert.That(overlay.IsVisible, Is.True);
                yield return WaitForApplicationState(ApplicationState.MainMenu);
                Assert.That(overlay.IsVisible, Is.False);
                Assert.That(UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)), Is.True);
            }
            finally { _loader.ContentSceneLoaded -= installed; }
        }
    }
}
#endif
