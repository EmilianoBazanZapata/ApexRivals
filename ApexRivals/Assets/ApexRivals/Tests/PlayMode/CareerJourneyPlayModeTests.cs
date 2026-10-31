#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Career.Configuration;
using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Configuration;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Configuration;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.Tracks.Configuration;
using ApexRivals.UI.Runtime;
using ApexRivals.VehicleSelection.Configuration;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    /// <summary>
    /// Fresh-profile career journey through the real MainMenu, Garage/Race Select and race scenes, with the
    /// real installers, UI and race setup. The profile lives in memory: no user save is read or written.
    /// </summary>
    public sealed class CareerJourneyPlayModeTests
    {
        private const string Content = "Assets/ApexRivals/Content/";
        private readonly HashSet<int> _originalScenes = new HashSet<int>();
        private Scene _originalActiveScene;
        private UnityContentSceneLoader _loader;
        private GameBootstrapper _bootstrapper;
        private SceneFlowService _flow;
        private MemoryStorage _storage;
        private CareerService _career;
        private CareerState _careerState;
        private PlayerProgressionState _progression;
        private SelectedVehicleState _selected;
        private bool _runInBackground;
        private bool _playFinaleIntroduction;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            _playFinaleIntroduction = false;
            Application.runInBackground = true;
            _originalScenes.Clear();
            _originalActiveScene = SceneManager.GetActiveScene();
            for (var i = 0; i < SceneManager.sceneCount; i++) _originalScenes.Add(SceneManager.GetSceneAt(i).handle);

            var bootstrapScene = SceneManager.CreateScene("CareerJourneyBootstrap");
            var root = new GameObject("CareerJourneyBootstrap");
            SceneManager.MoveGameObjectToScene(root, bootstrapScene);
            _loader = root.AddComponent<UnityContentSceneLoader>();
            _bootstrapper = root.AddComponent<GameBootstrapper>();
            // Startup is not run: it would open the real profile in persistentDataPath.
            _bootstrapper.enabled = false;
            _storage = new MemoryStorage();
            BuildContext();
            _loader.ContentSceneLoaded += OnLoaded;
            _loader.ContentSceneUnloading += OnUnloading;

            var load = CreateSaveService().LoadOrCreateDefault();
            Assert.That(load.Status, Is.EqualTo(PlayerProfileLoadStatus.CreatedDefault));
            _career.Initialize();
            var menu = _flow.OpenMainMenu();
            yield return WaitForTask(menu);
            Assert.That(menu.Result.Succeeded, Is.True, menu.Result.Message);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (_loader != null && _loader.IsLoading && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Time.timeScale = 1f;
            if (_loader != null)
            {
                _loader.ContentSceneLoaded -= OnLoaded;
                _loader.ContentSceneUnloading -= OnUnloading;
            }

            if (_originalActiveScene.IsValid() && _originalActiveScene.isLoaded) SceneManager.SetActiveScene(_originalActiveScene);
            var scenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!_originalScenes.Contains(scene.handle)) scenes.Add(scene);
            }

            foreach (var scene in scenes)
            {
                if (scene.isLoaded)
                {
                    var unload = SceneManager.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }

            Application.runInBackground = _runInBackground;
        }

        [UnityTest]
        public IEnumerator FreshCareerPlaysThroughUnlocksTheFinalAndSurvivesARestart()
        {
            // Main menu shows the career from the first frame.
            var menuPanel = FindInScene<CareerPanelUguiView>("MainMenu");
            Assert.That(menuPanel, Is.Not.Null, "Main menu career panel is wired.");
            Assert.That(TextOf(menuPanel, "CareerCompletionPercent"), Is.EqualTo("0%"));
            Assert.That(TextOf(menuPanel, "CareerObjectiveTitle"), Does.Contain("APEX CIRCUIT"));

            // Garage: Vanguard is granted and equipped; the Striker can be browsed but not bought yet.
            var garage = _flow.OpenTrackSelection();
            yield return WaitForTask(garage);
            Assert.That(garage.Result.Succeeded, Is.True, garage.Result.Message);
            yield return null;
            var garagePresenter = Field<GaragePresenter>(FindInScene<GarageUiSceneInstaller>("Garage"), "_garagePresenter");
            var trackSelect = Field<TrackSelectionPresenter>(FindInScene<GarageUiSceneInstaller>("Garage"), "_trackSelectionPresenter");
            garagePresenter.StartRace().Wait();
            Assert.That(_selected.SelectedVehicleId, Is.EqualTo("vanguard"));
            garagePresenter.EquipNextVehicle();
            Assert.That(garagePresenter.Current.Career.CandidateOwned, Is.False);
            Assert.That(garagePresenter.Current.Career.CandidateStatus, Does.Contain("REQUIRES: WIN AT APEX CIRCUIT"));
            Assert.That(garagePresenter.Current.Career.CanBuyCandidate, Is.False);
            Assert.That(_selected.SelectedVehicleId, Is.EqualTo("vanguard"), "Browsing a locked car never equips it.");

            // Race Select: Dunes is visible but locked, and the career rejects it at the service level.
            trackSelect.Open();
            var tracks = trackSelect.Current.Tracks;
            Assert.That(tracks.Count, Is.EqualTo(4), "Three circuits plus the final entry.");
            Assert.That(tracks[1].IsLocked, Is.True);
            Assert.That(tracks[3].IsFinale, Is.True);
            Assert.That(tracks[3].IsLocked, Is.True);
            trackSelect.SelectTrack(1);
            yield return WaitForTask(trackSelect.StartRace());
            Assert.That(_flow.State, Is.EqualTo(ApplicationState.Garage));
            Assert.That(trackSelect.Current.Feedback, Does.Contain("Win at Apex Circuit"));

            // Win Apex Circuit through the real race scene.
            trackSelect.SelectTrack(0);
            yield return StartFromTrackSelect(trackSelect, "Race");
            yield return FinishPlayer("Race", playerPosition: 1);
            var result = _flow.RaceSession.Result;
            Assert.That(result.CareerOutcome.Status, Is.EqualTo(CareerRaceOutcomeStatus.Recorded));
            Assert.That(result.CareerOutcome.NewlyUnlockedTracks, Does.Contain("Dunes Circuit"));
            Assert.That(result.SaveSucceeded, Is.True);
            var resultsView = FindInScene<ResultsUguiView>("Race");
            Assert.That(TextField(resultsView, "careerTitleText"), Is.EqualTo("CAREER 22%"));
            Assert.That(TextField(resultsView, "careerBodyText"), Does.Contain("NEW CIRCUIT UNLOCKED: DUNES CIRCUIT"));

            // Retrying does not count again; abandoning the retry records nothing.
            var currency = _progression.Currency;
            var retry = Session("Race").Retry();
            yield return WaitForTask(retry);
            Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
            yield return WaitForRacing("Race");
            Assert.That(_progression.Currency, Is.EqualTo(currency));
            Assert.That(_careerState.CompletedTrackIds, Is.EqualTo(new[] { "circuit" }));
            var back = Session("Race").ReturnToGarage();
            yield return WaitForTask(back);
            Assert.That(back.Result.Succeeded, Is.True, back.Result.Message);
            Assert.That(_progression.Currency, Is.EqualTo(currency));
            Assert.That(_careerState.CompletedTrackIds, Is.EqualTo(new[] { "circuit" }));

            // Qualify for the final (the remaining milestones are covered by EditMode tests) and lose it once.
            _careerState.Restore(new[] { "vanguard", "striker" }, new[] { "circuit", "dunes", "crossover" }, new[] { "circuit", "dunes", "crossover" }, new[] { "apex-finish", "apex-win" }, false);
            _progression.SetUpgradeLevel(UpgradeType.Engine, 1);
            _progression.SetUpgradeLevel(UpgradeType.Handling, 2);
            _career.EvaluateMilestones();
            Assert.That(_career.GetFinaleStatus().State, Is.EqualTo(CareerFinaleState.Available));
            trackSelect = Field<TrackSelectionPresenter>(FindInScene<GarageUiSceneInstaller>("Garage"), "_trackSelectionPresenter");
            trackSelect.Open();
            trackSelect.SelectTrack(3);
            Assert.That(trackSelect.Current.CanStartRace, Is.True);
            _playFinaleIntroduction = true;
            yield return StartFromTrackSelect(trackSelect, "RaceSuzuka");
            var installer = FindInScene<RaceSessionSceneInstaller>("RaceSuzuka");
            Assert.That(installer.CareerAttempt.IsFinale, Is.True);
            Assert.That(installer.CareerAttempt.TrackId, Is.EqualTo("crossover"));
            Assert.That(FindInScene<RaceCoordinator>("RaceSuzuka").Participants.Count, Is.EqualTo(4), "Fixed final line-up: player and three rivals.");
            yield return FinishPlayer("RaceSuzuka", playerPosition: 2);
            Assert.That(_flow.RaceSession.Result.CareerOutcome.IsFinale, Is.True);
            Assert.That(_careerState.FinaleWon, Is.False);
            Assert.That(TextField(FindInScene<ResultsUguiView>("RaceSuzuka"), "careerTitleText"), Does.Contain("FINAL LOST"));

            // Retry the final from results and win it.
            retry = Session("RaceSuzuka").Retry();
            yield return WaitForTask(retry);
            Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
            Assert.That(FindInScene<RaceSessionSceneInstaller>("RaceSuzuka").CareerAttempt.IsFinale, Is.True, "A retry keeps the final identity.");
            yield return WaitForRacing("RaceSuzuka");
            var beforeWin = _progression.Currency;
            yield return FinishPlayer("RaceSuzuka", playerPosition: 1);
            var win = _flow.RaceSession.Result;
            Assert.That(win.CareerOutcome.CareerCompleted, Is.True);
            Assert.That(win.UpdatedCurrency, Is.EqualTo(beforeWin + win.EarnedReward + 2000));
            Assert.That(TextField(FindInScene<ResultsUguiView>("RaceSuzuka"), "careerTitleText"), Is.EqualTo("CHAMPION! CAREER COMPLETE - 100%"));

            // Back to the menu: it refreshes; then a restart loads the same career from the saved profile.
            var toMenu = Session("RaceSuzuka").ReturnToMainMenu();
            yield return WaitForTask(toMenu);
            Assert.That(toMenu.Result.Succeeded, Is.True, toMenu.Result.Message);
            yield return null;
            Assert.That(TextOf(FindInScene<CareerPanelUguiView>("MainMenu"), "CareerCompletionPercent"), Is.EqualTo("100%"));

            var restartedState = new CareerState();
            var restartedProgression = new PlayerProgressionState();
            var restartLoad = new PlayerProfileSaveService(restartedProgression, new SelectedVehicleState(), _storage, new[] { "vanguard", "striker" }, restartedState).LoadOrCreateDefault();
            Assert.That(restartLoad.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded), restartLoad.Message);
            Assert.That(restartedState.FinaleWon, Is.True);
            Assert.That(restartedState.OwnedVehicleIds, Is.EquivalentTo(new[] { "vanguard", "striker" }));
            Assert.That(restartedProgression.Currency, Is.EqualTo(_progression.Currency));
        }

        private IEnumerator StartFromTrackSelect(TrackSelectionPresenter trackSelect, string sceneName)
        {
            var start = trackSelect.StartRace();
            yield return WaitForTask(start);
            Assert.That(_flow.State, Is.EqualTo(ApplicationState.Race), trackSelect.Current.Feedback + " " + trackSelect.Current.MessageKey);
            Assert.That(SceneManager.GetSceneByName(sceneName).isLoaded, Is.True);
            yield return WaitForRacing(sceneName);
        }

        private IEnumerator WaitForRacing(string sceneName)
        {
            var installer = FindInScene<RaceSessionSceneInstaller>(sceneName);
            Assert.That(installer, Is.Not.Null);
            Assert.That(installer.LastInstallResult.Succeeded, Is.True, installer.LastInstallResult.Message);
            var deadline = Time.realtimeSinceStartup + 10f;
            while ((installer.Session.State == RaceSessionLifecycleState.Starting || installer.Session.State == RaceSessionLifecycleState.Ready)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
        }

        /// <summary>Drives checkpoints directly; rivals finish first when the player should not win.</summary>
        private IEnumerator FinishPlayer(string sceneName, int playerPosition)
        {
            var coordinator = FindInScene<RaceCoordinator>(sceneName);
            var player = coordinator.PlayerParticipant;
            var rivalsAhead = playerPosition - 1;
            foreach (var participant in coordinator.Participants)
            {
                if (rivalsAhead == 0) break;
                if (participant == player) continue;
                PassAll(coordinator, participant);
                rivalsAhead--;
            }

            PassAll(coordinator, player);
            yield return null;
            Assert.That(Session(sceneName).State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
            Assert.That(_flow.RaceSession.Result.FinalPosition, Is.EqualTo(playerPosition));
        }

        private static void PassAll(RaceCoordinator coordinator, RaceParticipant participant)
        {
            var maximumPasses = coordinator.TotalLaps * coordinator.OrderedCheckpoints.Count + 1;
            for (var i = 0; i < maximumPasses; i++)
            {
                Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                if (progress.HasFinished) return;
                var pass = coordinator.TryPassCheckpoint(participant, coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex]);
                Assert.That(pass.Accepted, Is.True, $"{coordinator.gameObject.scene.name} {participant.name} pass {i}: {pass.Status}, race {coordinator.State}");
            }
        }

        private RaceSessionCoordinator Session(string sceneName)
        {
            return FindInScene<RaceSessionSceneInstaller>(sceneName).Session;
        }

        private void BuildContext()
        {
            var vehiclesAsset = AssetDatabase.LoadAssetAtPath<VehicleCatalogDefinition>(Content + "Configuration/Vehicles/VehicleCatalogDefinition.asset");
            var tracksAsset = AssetDatabase.LoadAssetAtPath<TrackCatalogDefinition>(Content + "Tracks/TrackCatalogDefinition.asset");
            var careerAsset = AssetDatabase.LoadAssetAtPath<CareerDefinition>(Content + "Configuration/Career/CareerDefinition.asset");
            var progressionAsset = AssetDatabase.LoadAssetAtPath<RaceProgressionDefinition>(Content + "Configuration/Progression/RaceProgressionDefinition.asset");
            var engine = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(Content + "Configuration/Garage/EngineUpgradeDefinition.asset").CreateData();
            var handling = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(Content + "Configuration/Garage/HandlingUpgradeDefinition.asset").CreateData();

            var vehicles = vehiclesAsset.CreateCatalog();
            var tracks = tracksAsset.CreateCatalog();
            _progression = new PlayerProgressionState();
            _selected = new SelectedVehicleState();
            _careerState = new CareerState();
            var save = CreateSaveService();
            var checkpoint = new PlayerProfileSaveCheckpoint(save);
            var configuration = careerAsset.CreateConfiguration();
            var validation = configuration.Validate(vehicles, tracks, new Dictionary<UpgradeType, int> { [UpgradeType.Engine] = engine.MaximumLevel, [UpgradeType.Handling] = handling.MaximumLevel });
            Assert.That(validation.Succeeded, Is.True, validation.Message);
            _career = new CareerService(configuration, _careerState, _progression, _selected, vehicles, tracks, checkpoint, careerAsset.FinaleRaceSetup);
            var selection = new VehicleSelectionService(vehicles, _selected, checkpoint, _progression, engine, handling, _career);
            var garage = new GarageService(_progression, engine, handling, () => selection.CurrentSelectedDefinition?.BasePerformanceStats ?? default);
            garage.UpgradePurchased += (_, _) => _career.EvaluateMilestones();
            _flow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), _loader, checkpoint, _career);
            var raceProgression = new RaceProgressionService(_progression, progressionAsset.CreateTable(), progressionAsset.CreateAiConfigurationMap());
            var context = new ApplicationContext(_flow, _progression, _selected, save, vehicles, selection, garage,
                new ApexRivals.Settings.Runtime.SettingsService(new MemorySettingsStorage(), new MemoryDisplaySettings()), raceProgression, career: _career);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_bootstrapper, context);
        }

        private PlayerProfileSaveService CreateSaveService()
        {
            return new PlayerProfileSaveService(_progression, _selected, _storage, new[] { "vanguard", "striker" }, _careerState);
        }

        private void OnLoaded(Scene scene)
        {
            // Career transactions are independent of the optional pre-race presentation.
            foreach (var root in scene.GetRootGameObjects())
            {
                var raceInstaller = root.GetComponentInChildren<RaceSessionSceneInstaller>(true);
                if (raceInstaller != null) raceInstaller.PlayIntroduction = _playFinaleIntroduction;
            }
            typeof(GameBootstrapper).GetMethod("OnContentSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_bootstrapper, new object[] { scene });
        }

        private void OnUnloading(Scene scene)
        {
            typeof(GameBootstrapper).GetMethod("OnContentSceneUnloading", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_bootstrapper, new object[] { scene });
        }

        private static T Field<T>(object owner, string name)
        {
            return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        }

        private static string TextField(Object view, string name)
        {
            var text = (Text)view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert.That(text, Is.Not.Null, name + " is wired.");
            return text.text;
        }

        private static string TextOf(Component root, string childName)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name == childName) return text.text;
            }

            Assert.Fail(childName + " not found.");
            return null;
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
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Scene operation timed out.");
            if (task.IsFaulted) Assert.Fail(task.Exception.ToString());
        }

        /// <summary>Keeps the profile in memory with the same primary/backup rotation as the file storage.</summary>
        private sealed class MemoryStorage : IPlayerProfileSaveStorage
        {
            private string _save;
            private string _backup;

            public bool SaveExists() => _save != null;
            public bool BackupExists() => _backup != null;
            public SaveStorageReadResult ReadSave() => new SaveStorageReadResult(_save != null, _save ?? string.Empty, _save != null ? string.Empty : "Missing.");
            public SaveStorageReadResult ReadBackup() => new SaveStorageReadResult(_backup != null, _backup ?? string.Empty, _backup != null ? string.Empty : "Missing.");

            public SaveStorageWriteResult WriteSave(string payload, bool preserveBackup = false)
            {
                if (_save != null && !preserveBackup) _backup = _save;
                _save = payload;
                return new SaveStorageWriteResult(true, string.Empty);
            }

            public SaveStorageWriteResult DeleteSave()
            {
                _save = null;
                _backup = null;
                return new SaveStorageWriteResult(true, string.Empty);
            }
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
    }
}
#endif
