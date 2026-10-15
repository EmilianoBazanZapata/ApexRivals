#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Configuration;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.VehicleSelection.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class PhaseOneSceneFlowPlayModeTests
    {
        private readonly HashSet<int> _originalScenes = new HashSet<int>();
        private Scene _originalActiveScene;
        private UnityContentSceneLoader _loader;
        private GameBootstrapper _bootstrapper;
        private SceneFlowService _flow;
        private MemoryStorage _storage;
        private int _raceInstallCount;

        private RaceSessionSceneInstaller Installer => FindInRace<RaceSessionSceneInstaller>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalScenes.Clear();
            _originalActiveScene = SceneManager.GetActiveScene();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                _originalScenes.Add(SceneManager.GetSceneAt(i).handle);
            }

            _raceInstallCount = 0;
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
            Assert.That(oldSession.Pause().Succeeded, Is.True);

            var retry = oldSession.Retry();
            yield return WaitForTask(retry);
            yield return null;

            AssertReplacement(oldSession, oldPlayer, retry.Result);
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

            var oldSession = Installer.Session;
            var oldPlayer = oldSession.PlayerVehicleRigidbody;
            Assert.That(oldSession.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
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
            if (scene.name != "Race") return;
            typeof(GameBootstrapper).GetMethod("OnContentSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_bootstrapper, new object[] { scene });
            _raceInstallCount++;
        }

        private void OnUnloading(Scene scene)
        {
            typeof(GameBootstrapper).GetMethod("OnContentSceneUnloading", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_bootstrapper, new object[] { scene });
        }

        private static T FindInRace<T>() where T : Component
        {
            var scene = SceneManager.GetSceneByName("Race");
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
