#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Garage.Configuration;
using ApexRivals.Progression.Configuration;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.VehicleSelection.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        private readonly HashSet<int> _originalScenes = new HashSet<int>();
        private Scene _originalActiveScene;
        private UnityContentSceneLoader _loader;
        private GameBootstrapper _bootstrapper;
        private SceneFlowService _flow;
        private MemoryStorage _storage;
        private int _raceInstallCount;
        private bool _installAllContentScenes;
        private InputSettings _originalInputSettings;
        private InputSettings _testInputSettings;

        private RaceSessionSceneInstaller Installer => FindInRace<RaceSessionSceneInstaller>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Synthetic input must reach the test even when MCP/Profiler changes
            // Editor focus. Keep this policy on a transient clone, never the asset.
            _originalInputSettings = InputSystem.settings;
            _testInputSettings = UnityEngine.Object.Instantiate(_originalInputSettings);
            _testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _testInputSettings;
            _originalScenes.Clear();
            _originalActiveScene = SceneManager.GetActiveScene();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                _originalScenes.Add(SceneManager.GetSceneAt(i).handle);
            }

            _raceInstallCount = 0;
            _installAllContentScenes = false;
            var bootstrapScene = SceneManager.CreateScene("PhaseOneTestBootstrap");
            var root = new GameObject("PhaseOneTestBootstrap");
            SceneManager.MoveGameObjectToScene(root, bootstrapScene);
            _loader = root.AddComponent<UnityContentSceneLoader>();
            _bootstrapper = root.AddComponent<GameBootstrapper>();
            // Use the real scene installer lifecycle with an in-memory profile. Do not
            // run startup, which constructs storage at Application.persistentDataPath.
            _bootstrapper.enabled = false;
            var progression = new PlayerProgressionState();
            var selected = new SelectedVehicleState("vanguard", true);
            _storage = new MemoryStorage();
            var save = new PlayerProfileSaveService(progression, selected, _storage, new[] { "vanguard", "striker" });
            var checkpoint = new PlayerProfileSaveCheckpoint(save);
            _flow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), _loader, checkpoint);
            var catalogAsset = AssetDatabase.LoadAssetAtPath<VehicleCatalogDefinition>(
                "Assets/ApexRivals/Content/Configuration/Vehicles/VehicleCatalogDefinition.asset");
            var progressionAsset = AssetDatabase.LoadAssetAtPath<RaceProgressionDefinition>(
                "Assets/ApexRivals/Content/Configuration/Progression/RaceProgressionDefinition.asset");
            Assert.That(catalogAsset, Is.Not.Null);
            Assert.That(progressionAsset, Is.Not.Null);
            var raceProgression = new RaceProgressionService(progression, progressionAsset.CreateTable(), progressionAsset.CreateAiConfigurationMap());
            var context = new ApplicationContext(_flow, progression, selected, save,
                catalogAsset.CreateCatalog(), raceProgressionService: raceProgression);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_bootstrapper, context);
            _loader.ContentSceneLoaded += OnLoaded;
            _loader.ContentSceneUnloading += OnUnloading;

            var menu = _flow.OpenMainMenu();
            yield return WaitForTask(menu);
            Assert.That(menu.Result.Succeeded, Is.True, menu.Result.Message);
            var start = _flow.StartRace();
            yield return WaitForTask(start);
            Assert.That(start.Result.Succeeded, Is.True, start.Result.Message);
            Assert.That(Installer, Is.Not.Null);
            Assert.That(Installer.LastInstallResult.Succeeded, Is.True, Installer.LastInstallResult.Message);
            var deadline = Time.realtimeSinceStartup + 8f;
            while (Installer.Session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // A failed assertion can interrupt a test before it awaits Retry.
            // Let the real loader finish before unloading fixture-owned scenes.
            var loadDeadline = Time.realtimeSinceStartupAsDouble + 20;
            while (_loader != null && _loader.IsLoading && Time.realtimeSinceStartupAsDouble < loadDeadline) yield return null;
            InputSystem.settings = _originalInputSettings;
            UnityEngine.Object.DestroyImmediate(_testInputSettings);
            _testInputSettings = null;
            _originalInputSettings = null;
            Time.timeScale = 1f;
            if (_loader != null)
            {
                _loader.ContentSceneLoaded -= OnLoaded;
                _loader.ContentSceneUnloading -= OnUnloading;
            }

            if (_originalActiveScene.IsValid() && _originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(_originalActiveScene);
            }

            var scenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!_originalScenes.Contains(scene.handle)) scenes.Add(scene);
            }

            foreach (var scene in scenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    var unload = SceneManager.UnloadSceneAsync(scene);
                    if (unload != null) yield return unload;
                }
            }

            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator RetryFromPauseReplacesTheSessionOnce()
        {
            var oldSession = Installer.Session;
            var oldPlayer = oldSession.PlayerVehicleRigidbody;
            var oldView = FindInRace<PauseUguiView>();
            var presenter = (PausePresenter)typeof(PauseUguiView).GetField("_presenter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(oldView);
            Assert.That(oldSession.Pause().Succeeded, Is.True);

            var retry = presenter.Retry();
            var duplicate = presenter.Retry();
            yield return WaitForTask(retry);
            yield return WaitForTask(duplicate);
            yield return null;

            AssertReplacement(oldSession, oldPlayer);
            Assert.That(typeof(PausePresenter).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter), Is.Null);
            Assert.That(typeof(PauseUguiView).GetField("_presenter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(oldView), Is.Null);
            var replacement = Installer.Session;
            presenter.Present();
            presenter.Pause();
            Assert.That(Installer.Session, Is.SameAs(replacement));
            Assert.That(replacement.State, Is.Not.EqualTo(RaceSessionLifecycleState.Paused));
        }

        [UnityTest]
        public IEnumerator PauseInputCharacterizationPreservesControlsDebounceAndCountdown()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                var session = Installer.Session;
                Time.timeScale = 0.5f;
                PressPause(keyboard);
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Paused));
                Assert.That(Time.timeScale, Is.Zero);
                PressPause(gamepad);
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Paused), "Immediate duplicate is debounced.");
                yield return new WaitForSecondsRealtime(0.22f);
                PressPause(keyboard);
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Paused), "Escape is handled by the UI cancel path while normally paused.");
                PressPause(gamepad);
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
                Assert.That(Time.timeScale, Is.EqualTo(0.5f));

                for (int cycle = 0; cycle < 3; cycle++)
                {
                    session.Pause();
                    Assert.That(session.Resume().Succeeded, Is.True);
                    var retry = session.Retry();
                    yield return WaitForTask(retry);
                    Assert.That(retry.Result.Succeeded, Is.True);
                    session = Installer.Session;
                    Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
                    PressPause(gamepad);
                    Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Paused));
                    Assert.That(session.Resume().Succeeded, Is.True);
                    Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
                    var deadline = Time.realtimeSinceStartup + 8f;
                    while (session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
                }
                Assert.That(_raceInstallCount, Is.EqualTo(4));
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
                InputSystem.RemoveDevice(keyboard);
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator FallbackPauseCharacterizationPreservesRejectedStatesAndUnload()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                var session = Installer.Session;
                var ui = FindInRace<RaceUiSceneInstaller>();
                var setState = typeof(RaceSessionCoordinator).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic);
                // Arrange rejected states directly to characterize the UI boundary;
                // normal domain transitions and save failures have separate integration tests.
                foreach (var state in new[] { RaceSessionLifecycleState.Uninitialized, RaceSessionLifecycleState.Preparing,
                    RaceSessionLifecycleState.Ready, RaceSessionLifecycleState.Failed,
                    RaceSessionLifecycleState.Retrying, RaceSessionLifecycleState.Exiting })
                {
                    setState.Invoke(session, new object[] { state });
                    Time.timeScale = 0.4f;
                    yield return new WaitForSecondsRealtime(0.22f);
                    PressPause(gamepad);
                    Assert.That(session.State, Is.EqualTo(state));
                    Assert.That(Time.timeScale, Is.Zero, state.ToString());
                    var view = FindInRace<PauseUguiView>();
                    Assert.That(view.gameObject.activeInHierarchy, Is.True);
                    Assert.That(ReadReference<Button>(view, "retryButton").interactable, Is.False);
                    ReadReference<Button>(view, "resumeButton").onClick.Invoke();
                    Assert.That(Time.timeScale, Is.EqualTo(0.4f));
                    Assert.That(view.gameObject.activeSelf, Is.False);
                }
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(0.22f);
                PressPause(gamepad);
                Assert.That(Time.timeScale, Is.Zero);
                ui.enabled = false;
                Assert.That(Time.timeScale, Is.EqualTo(1f), "A zero previous scale uses the existing fallback of one.");
                ui.enabled = true;
                Time.timeScale = 0.6f;
                PressPause(gamepad);
                Assert.That(Time.timeScale, Is.Zero, "Re-enable creates one working input subscription.");
                ui.Uninstall();
                ui.Uninstall();
                Assert.That(Time.timeScale, Is.EqualTo(0.6f));
                setState.Invoke(session, new object[] { RaceSessionLifecycleState.Racing });
            }
            finally { InputSystem.RemoveDevice(gamepad); Time.timeScale = 1f; }
        }

        [UnityTest]
        public IEnumerator PauseDuringTransitionCharacterizesFallbackBeforeReplacement()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var session = Installer.Session;
                var retry = session.Retry();
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Retrying));
                PressPause(keyboard);
                Assert.That(Time.timeScale, Is.Zero);
                var view = FindInRace<PauseUguiView>();
                Assert.That(view.gameObject.activeSelf, Is.True);
                ReadReference<Button>(view, "resumeButton").onClick.Invoke();
                Assert.That(Time.timeScale, Is.Zero, "Dismissing fallback must not release the pending Retry's freeze.");
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True);
                Assert.That(Installer.Session, Is.Not.SameAs(session));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally { InputSystem.RemoveDevice(keyboard); Time.timeScale = 1f; }
        }

        private static void PressPause(InputDevice device)
        {
            if (device is Keyboard keyboard)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            else if (device is Gamepad gamepad)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
                InputSystem.Update();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
            }
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator HudTracksClockProgressVisibilityResultsAndRetryCountdown()
        {
            var session = Installer.Session;
            var hud = FindInRace<RaceHudUguiView>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.gameObject.activeInHierarchy, Is.True);
            hud.SendMessage("Update");
            AssertHudMatchesSnapshot(hud, session.Hud);
            var initialTime = session.Hud.RaceTime;
            yield return new WaitForSeconds(0.1f);
            Assert.That(session.Hud.RaceTime, Is.GreaterThan(initialTime));
            hud.gameObject.SetActive(false);
            yield return null;
            hud.gameObject.SetActive(true);
            yield return null;
            hud.SendMessage("Update");
            AssertHudMatchesSnapshot(hud, session.Hud);

            var coordinator = FindInRace<RaceCoordinator>();
            var participant = coordinator.PlayerParticipant;
            var observedLapChange = false;
            var initialLap = session.Hud.CurrentLap;
            var maximumPasses = coordinator.TotalLaps * coordinator.OrderedCheckpoints.Count + 1;
            for (var index = 0; index < maximumPasses; index++)
            {
                Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                if (progress.HasFinished) break;
                Assert.That(coordinator.TryPassCheckpoint(participant, coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex]).Accepted, Is.True);
                if (hud.isActiveAndEnabled) hud.SendMessage("Update");
                AssertHudMatchesSnapshot(hud, session.Hud);
                observedLapChange |= session.Hud.CurrentLap != initialLap;
            }

            Assert.That(observedLapChange, Is.EqualTo(coordinator.TotalLaps > 1));
            Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
            Assert.That(session.LastResults.FinishTime, Is.EqualTo(session.Hud.RaceTime));
            Assert.That(hud.gameObject.activeInHierarchy, Is.False);
            var retry = session.Retry();
            yield return WaitForTask(retry);
            Assert.That(retry.Result.Succeeded, Is.True);
            var replacement = Installer.Session;
            var replacementHud = FindInRace<RaceHudUguiView>();
            Assert.That(replacement, Is.Not.SameAs(session));
            Assert.That(replacementHud, Is.Not.Null);
            Assert.That(replacement.Hud.CountdownRemaining, Is.GreaterThan(0f));
            replacementHud.SendMessage("Update");
            AssertHudMatchesSnapshot(replacementHud, replacement.Hud);
            var countdown = replacement.Hud.CountdownRemaining;
            var deadline = Time.realtimeSinceStartup + 8f;
            while (replacement.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(replacement.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
            replacementHud.SendMessage("Update");
            AssertHudMatchesSnapshot(replacementHud, replacement.Hud);
            SessionState.SetString("ApexRivals.Cleanup2.HudReadback", $"clock advanced; lap/progress matched; hide/reactivate passed; finish={session.LastResults.FinishTime}; retry countdown={countdown}; race started; HUD text matches live snapshots");
        }

        private static void AssertHudMatchesSnapshot(RaceHudUguiView hud, RaceHudSnapshot snapshot)
        {
            using var serialized = new SerializedObject(hud);
            var countdown = serialized.FindProperty("countdownText").objectReferenceValue as Text;
            var lap = serialized.FindProperty("lapText").objectReferenceValue as Text;
            var position = serialized.FindProperty("positionText").objectReferenceValue as Text;
            var time = serialized.FindProperty("timeText").objectReferenceValue as Text;
            Assert.That(countdown, Is.Not.Null);
            Assert.That(lap, Is.Not.Null);
            Assert.That(position, Is.Not.Null);
            Assert.That(time, Is.Not.Null);
            Assert.That(countdown.text, Is.EqualTo(snapshot.CountdownRemaining > 0f ? Mathf.CeilToInt(snapshot.CountdownRemaining).ToString() : string.Empty));
            Assert.That(lap.text, Is.EqualTo($"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps}"));
            Assert.That(position.text, Is.EqualTo($"{snapshot.CurrentPosition}/{snapshot.ParticipantCount}"));
            var minutes = (int)(snapshot.RaceTime / 60f);
            Assert.That(time.text, Is.EqualTo(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:00}:{1:00.000}", minutes, snapshot.RaceTime - minutes * 60f)));
        }

        [UnityTest]
        public IEnumerator AuthoredUiJourneyKeepsKeyboardGamepadAndScreenFocus()
        {
            var original = _bootstrapper.Context;
            var engine = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/EngineUpgradeDefinition.asset");
            var handling = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/HandlingUpgradeDefinition.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(engine, Is.Not.Null);
            Assert.That(handling, Is.Not.Null);
            Assert.That(actions, Is.Not.Null);
            var selection = new ApexRivals.VehicleSelection.Runtime.VehicleSelectionService(original.VehicleCatalog, original.SelectedVehicleState,
                new PlayerProfileSaveCheckpoint(original.SaveService), original.ProgressionState, engine.CreateData(), handling.CreateData());
            var garage = new GarageService(original.ProgressionState, engine.CreateData(), handling.CreateData(), () => selection.CurrentSelectedDefinition.BasePerformanceStats);
            var context = new ApplicationContext(_flow, original.ProgressionState, original.SelectedVehicleState, original.SaveService,
                original.VehicleCatalog, selection, garage, new ApexRivals.Settings.Runtime.SettingsService(new MemorySettingsStorage(), new MemoryDisplaySettings()), original.RaceProgressionService, original.PauseCoordinator);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_bootstrapper, context);
            _installAllContentScenes = true;
            var uiInput = _bootstrapper.gameObject.AddComponent<BootstrapUiInputSystem>();
            using (var serialized = new SerializedObject(uiInput))
            {
                serialized.FindProperty("inputActions").objectReferenceValue = actions;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            uiInput.SendMessage("ConfigureInputModule");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                var exit = Installer.Session.ReturnToMainMenu();
                yield return WaitForTask(exit);
                Assert.That(exit.Result.Succeeded, Is.True);
                yield return null;
                var menu = FindInScene<MainMenuUguiView>("MainMenu");
                Assert.That(menu, Is.Not.Null);
                var start = ReadReference<Button>(menu, "startButton");
                Assert.That(UiFocus.Request(start), Is.True);
                var keyboardTarget = start.navigation.selectOnDown;
                Assert.That(keyboardTarget, Is.Not.Null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(keyboardTarget.gameObject));
                var gamepadTarget = keyboardTarget.navigation.selectOnDown;
                Assert.That(gamepadTarget, Is.Not.Null);
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown));
                yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(gamepadTarget.gameObject));

                ReadReference<Button>(menu, "settingsButton").onClick.Invoke();
                yield return null;
                var navigator = FindInScene<UguiScreenNavigator>("MainMenu");
                Assert.That(navigator.CurrentScreen, Is.EqualTo(PresentationScreenState.Settings));
                Assert.That(UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)), Is.True);
                var settings = FindInScene<SettingsUguiView>("MainMenu");
                ReadReference<Button>(settings, "cancelButton").onClick.Invoke();
                yield return null;
                Assert.That(navigator.CurrentScreen, Is.EqualTo(PresentationScreenState.Main));
                ReadReference<Button>(menu, "garageButton").onClick.Invoke();
                yield return WaitForApplicationState(ApplicationState.Garage);
                var garageView = FindInScene<GarageUguiView>("Garage");
                Assert.That(garageView, Is.Not.Null);
                Assert.That(UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)), Is.True);
                ReadReference<Button>(garageView, "startRaceButton").onClick.Invoke();
                yield return WaitForApplicationState(ApplicationState.Race);
                var session = Installer.Session;
                var deadline = Time.realtimeSinceStartup + 8f;
                while (session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(session.Pause().Succeeded, Is.True);
                yield return null;
                Assert.That(FindInRace<UguiScreenNavigator>().CurrentScreen, Is.EqualTo(PresentationScreenState.Paused));
                Assert.That(UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)), Is.True);
                Assert.That(session.Resume().Succeeded, Is.True);
                var coordinator = FindInRace<RaceCoordinator>();
                var participant = coordinator.PlayerParticipant;
                for (var index = 0; index < coordinator.TotalLaps * coordinator.OrderedCheckpoints.Count + 1; index++)
                {
                    Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                    if (progress.HasFinished) break;
                    Assert.That(coordinator.TryPassCheckpoint(participant, coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex]).Accepted, Is.True);
                }
                yield return null;
                Assert.That(FindInRace<UguiScreenNavigator>().CurrentScreen, Is.EqualTo(PresentationScreenState.Results));
                Assert.That(UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)), Is.True);
                SessionState.SetString("ApexRivals.Cleanup2.UiJourney", "MainMenu > Settings > MainMenu > Garage > Race > Pause > Results; keyboard and gamepad moved focus along authored links; every menu selection usable");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [UnityTest]
        public IEnumerator VehicleFactoryRejectsPrefabWithoutCompositionAndDestroysItsInstance()
        {
            var prefab = new GameObject("InvalidRaceSetupPrefab");
            SceneManager.MoveGameObjectToScene(prefab, _bootstrapper.gameObject.scene);
            var factory = new ApexRivals.RaceSetup.Runtime.UnityRaceVehicleFactory();

            var created = factory.TryCreate(prefab, new ApexRivals.RaceSetup.Runtime.SpawnPose(Vector3.zero, Quaternion.identity), out var composition, out var message);

            Assert.That(created, Is.False);
            Assert.That(composition, Is.Null);
            Assert.That(message, Is.EqualTo("The vehicle prefab root is missing RaceVehicleComposition."));
            yield return null;
            Assert.That(GameObject.Find("InvalidRaceSetupPrefab(Clone)"), Is.Null);
        }

        private IEnumerator WaitForApplicationState(ApplicationState state)
        {
            var deadline = Time.realtimeSinceStartup + 8f;
            while (_flow.State != state && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_flow.State, Is.EqualTo(state));
            yield return null;
        }

        private static T ReadReference<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object
        {
            Assert.That(target, Is.Not.Null);
            using var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field);
            var reference = property.objectReferenceValue as T;
            Assert.That(reference, Is.Not.Null, field);
            return reference;
        }

        private sealed class MemorySettingsStorage : ISettingsStorage
        {
            public bool SettingsExist() => false;
            public SettingsStorageReadResult Load() => new SettingsStorageReadResult(SettingsStorageReadStatus.Missing, default, string.Empty);
            public SettingsStorageWriteResult Save(SettingsState settings) => new SettingsStorageWriteResult(true, string.Empty);
            public SettingsStorageWriteResult RestoreDefaults(SettingsState defaults) => new SettingsStorageWriteResult(true, string.Empty);
        }

        private sealed class MemoryDisplaySettings : IDisplaySettingsAdapter
        {
            public IReadOnlyList<SettingsResolution> SupportedResolutions => new[] { SettingsState.Default.Resolution };
            public SettingsResolution CurrentResolution => SettingsState.Default.Resolution;
            public SettingsOperationResult Apply(SettingsState settings) => new SettingsOperationResult(SettingsOperationStatus.Succeeded, string.Empty);
        }

        [UnityTest]
        public IEnumerator RealRaceSetupUsesRouteProgressForCurvePositions()
        {
            var coordinator = FindInRace<RaceCoordinator>();
            var line = FindInRace<ApexRivals.AI.Runtime.RacingLine>();
            Assert.That(line, Is.Not.Null);
            var scene = SceneManager.GetSceneByName("Race");
            var racers = new List<RaceParticipant>();
            foreach (var root in scene.GetRootGameObjects()) racers.AddRange(root.GetComponentsInChildren<RaceParticipant>());
            var player = coordinator.PlayerParticipant;
            var opponent = racers.Find(r => r != player);
            Assert.That(opponent, Is.Not.Null);
            foreach (var racer in new[] { player, opponent })
            {
                Assert.That(coordinator.TryPassCheckpoint(racer, coordinator.OrderedCheckpoints[1]).Accepted, Is.True);
            }
            var start = line.GetWaypointPosition(13);
            var end = line.GetWaypointPosition(14);
            var side = Vector3.Cross(Vector3.up, (end - start).normalized);
            player.transform.position = Vector3.Lerp(start, end, 0.6f) + side * 5f;
            opponent.transform.position = Vector3.Lerp(start, end, 0.4f) - side * 5f;
            coordinator.SendMessage("RecalculatePositions");
            Assert.That(coordinator.GetLivePosition(player.RacerId), Is.LessThan(coordinator.GetLivePosition(opponent.RacerId)));
            foreach (var checkpoint in coordinator.OrderedCheckpoints)
            {
                var box = checkpoint.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null);
                Assert.That(box.isTrigger, Is.True);
                Assert.That(box.size.z * Mathf.Abs(box.transform.lossyScale.z), Is.GreaterThanOrEqualTo(3f - 0.001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetryFromResultsReplacesTheSessionWithoutAwardingAgain()
        {
            var oldSession = Installer.Session;
            var resultsNotifications = 0;
            oldSession.ResultsReady += result =>
            {
                resultsNotifications++;
                Assert.That(_flow.RaceSession.Result, Is.EqualTo(result));
                Assert.That(oldSession.LastResults, Is.EqualTo(result));
                Assert.That(oldSession.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
                Assert.That(_flow.RacePresentationState, Is.EqualTo(RacePresentationState.Results));
            };
            var coordinator = FindInRace<RaceCoordinator>();
            var participant = coordinator.PlayerParticipant;
            var maximumPasses = coordinator.TotalLaps * coordinator.OrderedCheckpoints.Count + 1;
            for (var i = 0; i < maximumPasses; i++)
            {
                Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                if (progress.HasFinished) break;
                var pass = coordinator.TryPassCheckpoint(participant, coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex]);
                Assert.That(pass.Accepted, Is.True);
            }

            var oldPlayer = oldSession.PlayerVehicleRigidbody;
            Assert.That(resultsNotifications, Is.EqualTo(1));
            Assert.That(oldSession.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                float previousTimeScale = Time.timeScale;
                PressPause(keyboard);
                PressPause(gamepad);
                Assert.That(oldSession.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
                Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale));
                Assert.That(FindInRace<PauseUguiView>().gameObject.activeSelf, Is.False);
            }
            finally { InputSystem.RemoveDevice(gamepad); InputSystem.RemoveDevice(keyboard); }
            var progression = _bootstrapper.Context.ProgressionState;
            var currency = progression.Currency;
            var completed = progression.CompletedRaceCount;
            var retry = oldSession.Retry();
            yield return WaitForTask(retry);
            yield return null;

            AssertReplacement(oldSession, oldPlayer, retry.Result);
            Assert.That(progression.Currency, Is.EqualTo(currency));
            Assert.That(progression.CompletedRaceCount, Is.EqualTo(completed));
            Assert.That(_flow.RaceSession.HasResult, Is.False);
        }

        [UnityTest]
        public IEnumerator FailedSaveDuringExitKeepsPausedVehiclesAndAllowsResumeAndExit()
        {
            var session = Installer.Session;
            var player = session.PlayerVehicleRigidbody;
            session.Pause();
            _storage.FailWrites = true;
            var failed = session.ReturnToMainMenu();
            yield return WaitForTask(failed);

            Assert.That(failed.Result.Status, Is.EqualTo(RaceSessionCommandStatus.NavigationFailed));
            Assert.That(Installer.Session, Is.SameAs(session));
            Assert.That(session.PlayerVehicleRigidbody, Is.EqualTo(player));
            Assert.That(player != null, Is.True);
            Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(session.Resume().Succeeded, Is.True);
            _storage.FailWrites = false;
            var exit = session.ReturnToMainMenu();
            yield return WaitForTask(exit);
            Assert.That(exit.Result.Succeeded, Is.True, exit.Result.Message);
            Assert.That(_flow.State, Is.EqualTo(ApplicationState.MainMenu));
        }

        [UnityTest]
        public IEnumerator SaveExceptionDuringRetryKeepsRaceUsable()
        {
            var session = Installer.Session;
            var player = session.PlayerVehicleRigidbody;
            _storage.ThrowWrites = true;
            var failed = session.Retry();
            yield return WaitForTask(failed);

            Assert.That(failed.Result.Succeeded, Is.False);
            Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
            Assert.That(session.PlayerVehicleRigidbody, Is.EqualTo(player));
            Assert.That(player != null, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_flow.State, Is.EqualTo(ApplicationState.Race));
            _storage.ThrowWrites = false;
            var retry = session.Retry();
            yield return WaitForTask(retry);
            Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
        }

        [UnityTest]
        public IEnumerator UnavailableSceneDoesNotBlockTheNextLoad()
        {
            var failed = _loader.LoadContentSceneAsync(ContentSceneId.Garage, "PhaseOneMissingScene", false);
            yield return WaitForTask(failed);
            Assert.That(failed.Result.Succeeded, Is.False);
            Assert.That(_loader.IsLoading, Is.False);
            var next = Installer.Session.ReturnToMainMenu();
            yield return WaitForTask(next);
            Assert.That(next.Result.Succeeded, Is.True, next.Result.Message);
        }

        [UnityTest]
        public IEnumerator ThrowingSceneObserverDoesNotStrandTheOperationOrSkipOtherObservers()
        {
            var notified = false;
            Action<Scene> throwing = _ => throw new InvalidOperationException("Controlled scene observer failure.");
            Action<Scene> following = _ => notified = true;
            _loader.ContentSceneLoaded += throwing;
            _loader.ContentSceneLoaded += following;
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: Controlled scene observer failure"));
            try
            {
                var retry = Installer.Session.Retry();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                Assert.That(notified, Is.True);
                Assert.That(_loader.IsLoading, Is.False);
                Assert.That(Installer.LastInstallResult.Succeeded, Is.True);
            }
            finally
            {
                _loader.ContentSceneLoaded -= throwing;
                _loader.ContentSceneLoaded -= following;
            }
        }

        [UnityTest]
        public IEnumerator CancelledInFlightLoadRemovesCandidateAndKeepsCurrentSession()
        {
            var session = Installer.Session;
            using (var cancellation = new CancellationTokenSource())
            {
                var cancelled = _loader.LoadContentSceneAsync(ContentSceneId.Garage, "Garage", false, cancellation.Token);
                cancellation.Cancel();
                yield return WaitForTask(cancelled);
                Assert.That(cancelled.Result.Succeeded, Is.False);
                Assert.That(_loader.IsLoading, Is.False);
                Assert.That(SceneManager.GetSceneByName("Garage").isLoaded, Is.False);
                Assert.That(Installer.Session, Is.SameAs(session));
                Assert.That(_loader.CurrentContentSceneName, Is.EqualTo("Race"));
                var next = session.ReturnToMainMenu();
                yield return WaitForTask(next);
                Assert.That(next.Result.Succeeded, Is.True, next.Result.Message);
            }
        }

        private void AssertReplacement(RaceSessionCoordinator oldSession, Rigidbody oldPlayer, RaceSessionCommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            AssertReplacement(oldSession, oldPlayer);
        }

        private void AssertReplacement(RaceSessionCoordinator oldSession, Rigidbody oldPlayer)
        {
            Assert.That(oldSession.State, Is.EqualTo(RaceSessionLifecycleState.Disposed));
            Assert.That(oldPlayer == null, Is.True);
            Assert.That(Installer.Session, Is.Not.SameAs(oldSession));
            Assert.That(Installer.LastInstallResult.Succeeded, Is.True, Installer.LastInstallResult.Message);
            Assert.That(Installer.Session.State == RaceSessionLifecycleState.Starting || Installer.Session.State == RaceSessionLifecycleState.Racing, Is.True);
            Assert.That(_raceInstallCount, Is.EqualTo(2));
            var raceCount = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == "Race") raceCount++;
            Assert.That(raceCount, Is.EqualTo(1));
            Assert.That(FindInRace<RaceCoordinator>().PlayerParticipant, Is.Not.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        private void OnLoaded(Scene scene)
        {
            // The menu is only a stepping stone; exercise the real bootstrap install
            // and uninstall methods for Race, including its UI and vehicle factory.
            if (scene.name != "Race" && !_installAllContentScenes) return;
            typeof(GameBootstrapper).GetMethod("OnContentSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_bootstrapper, new object[] { scene });
            if (scene.name == "Race") _raceInstallCount++;
        }

        private void OnUnloading(Scene scene)
        {
            typeof(GameBootstrapper).GetMethod("OnContentSceneUnloading", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_bootstrapper, new object[] { scene });
        }

        private static T FindInRace<T>() where T : Component
        {
            return FindInScene<T>("Race");
        }

        private static T FindInScene<T>(string sceneName) where T : Component
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }

            return null;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Scene operation timed out.");
            if (task.IsFaulted) Assert.Fail(task.Exception.ToString());
        }

        private sealed class MemoryStorage : IPlayerProfileSaveStorage
        {
            public bool FailWrites;
            public bool ThrowWrites;
            public bool SaveExists() => false;
            public bool BackupExists() => false;
            public SaveStorageReadResult ReadSave() => default;
            public SaveStorageReadResult ReadBackup() => default;
            public SaveStorageWriteResult WriteSave(string payload, bool preserveBackup = false)
            {
                if (ThrowWrites) throw new InvalidOperationException("Controlled storage exception.");
                return new SaveStorageWriteResult(!FailWrites, FailWrites ? "Controlled storage failure." : string.Empty);
            }

            public SaveStorageWriteResult DeleteSave() => new SaveStorageWriteResult(true, string.Empty);
        }
    }
}
#endif
