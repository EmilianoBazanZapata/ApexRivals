#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Garage.Configuration;
using ApexRivals.Garage.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        // Diagnostic checkpoints intentionally collect/unload. These are ownership
        // tests in the Editor, not representative gameplay frame-time benchmarks.
        [Serializable]
        private sealed class StabilitySample
        {
            public int iteration;
            public double elapsedSeconds;
            public double transitionSeconds;
            public long managedLiveBytes;
            public long monoHeapBytes;
            public long unityAllocatedBytes;
            public long unityReservedBytes;
            public int objects;
            public int gameObjects;
            public int materials;
            public int meshes;
            public int textures;
            public int renderTextures;
            public int audioSources;
            public int audioClips;
            public int raceInstallers;
            public int participants;
            public int navigationListeners;
            public int sessionStateListeners;
            public int sessionResultListeners;
            public int sessionPositionListeners;
            public int retainedSessions;
            public int retainedPresenters;
            public float timeScale;
        }

        [Serializable]
        private sealed class StabilityRun
        {
            public string unity;
            public string environment = "Editor PlayMode; in-memory saves; authored roster; checkpoint-driven finish; forced GC/unload checkpoints";
            public string scenario;
            public int completedIterations;
            public List<string> nativeSnapshots = new List<string>();
            public List<StabilitySample> samples = new List<StabilitySample>();
        }

        [UnityTest, Category("Phase5Stress"), Timeout(600000)]
        public IEnumerator TwentySceneCyclesReleaseSessionsAndPresenters()
        {
            ConfigureStabilityMenus();
            var run = new StabilityRun { unity = Application.unityVersion, scenario = "MainMenu-Garage-Race-Results-MainMenu x20" };
            var sessions = new List<WeakReference>();
            var presenters = new List<WeakReference>();
            var started = Time.realtimeSinceStartupAsDouble;
            yield return WaitForTask(Installer.Session.ReturnToMainMenu());
            try
            {
                for (var iteration = 1; iteration <= 20; iteration++)
                {
                    var transitionStart = Time.realtimeSinceStartupAsDouble;
                    var garage = _flow.OpenGarage();
                    yield return WaitForTask(garage);
                    Assert.That(garage.Result.Succeeded, Is.True, garage.Result.Message);
                    Assert.That(FindInScene<GarageUguiView>("Garage"), Is.Not.Null);
                    var race = _flow.StartRace();
                    yield return WaitForTask(race);
                    Assert.That(race.Result.Succeeded, Is.True, race.Result.Message);
                    yield return WaitForStabilityCountdown();
                    AssertSingleStabilityRace();
                    Assert.That(Installer.Session.Pause().Succeeded, Is.True);
                    Assert.That(Time.timeScale, Is.Zero);
                    Assert.That(Installer.Session.Resume().Succeeded, Is.True);
                    CompleteStabilityRace();
                    Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
                    var old = TrackStabilityAttempt(sessions, presenters);
                    var exit = Installer.Session.ReturnToMainMenu();
                    yield return WaitForTask(exit);
                    Assert.That(exit.Result.Succeeded, Is.True, exit.Result.Message);
                    yield return null;
                    AssertReleasedStabilityAttempt(old);
                    old = null;
                    Assert.That(FindInRace<RaceSessionSceneInstaller>(), Is.Null);
                    Assert.That(Time.timeScale, Is.EqualTo(1f));
                    var elapsed = Time.realtimeSinceStartupAsDouble - transitionStart;
                    yield return CollectStabilityCheckpoint();
                    run.completedIterations = iteration;
                    var sample = CaptureStabilitySample(iteration, started, elapsed, sessions, presenters);
                    AssertStabilityOwnership(sample, false);
                    run.samples.Add(sample);
                    if (SessionState.GetBool("ApexRivals.Phase5.CaptureSnapshots", false) && (iteration == 3 || iteration == 20))
                        yield return CaptureStabilityNativeSnapshot(iteration, run);
                    WriteStabilityRun("phase5-scene-cycles.json", run);
                }
            }
            finally
            {
                SessionState.SetBool("ApexRivals.Phase5.CaptureSnapshots", false);
                WriteStabilityRun("phase5-scene-cycles.json", run);
            }
        }

        [UnityTest, Category("Phase5Stress"), Timeout(600000)]
        public IEnumerator FiftyRetriesReleaseOldAttemptsAndKeepOneRace()
        {
            var run = new StabilityRun { unity = Application.unityVersion, scenario = "Race Retry x50; alternating Racing/Paused/Results" };
            var sessions = new List<WeakReference>();
            var presenters = new List<WeakReference>();
            var started = Time.realtimeSinceStartupAsDouble;
            try
            {
                for (var iteration = 1; iteration <= 50; iteration++)
                {
                    yield return WaitForStabilityCountdown();
                    AssertSingleStabilityRace();
                    if (iteration % 3 == 1) Assert.That(Installer.Session.Pause().Succeeded, Is.True);
                    if (iteration % 3 == 2) CompleteStabilityRace();
                    var old = TrackStabilityAttempt(sessions, presenters);
                    var transitionStart = Time.realtimeSinceStartupAsDouble;
                    var retry = Installer.Session.Retry();
                    yield return WaitForTask(retry);
                    Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                    yield return null;
                    AssertReleasedStabilityAttempt(old);
                    old = null;
                    AssertSingleStabilityRace();
                    var elapsed = Time.realtimeSinceStartupAsDouble - transitionStart;
                    yield return WaitForStabilityCountdown();
                    yield return CollectStabilityCheckpoint();
                    run.completedIterations = iteration;
                    var sample = CaptureStabilitySample(iteration, started, elapsed, sessions, presenters);
                    AssertStabilityOwnership(sample, true);
                    run.samples.Add(sample);
                    WriteStabilityRun("phase5-retries.json", run);
                }
            }
            finally { WriteStabilityRun("phase5-retries.json", run); }
        }

        private void ConfigureStabilityMenus()
        {
            var original = _bootstrapper.Context;
            var engine = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/EngineUpgradeDefinition.asset");
            var handling = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/HandlingUpgradeDefinition.asset");
            var selection = new ApexRivals.VehicleSelection.Runtime.VehicleSelectionService(original.VehicleCatalog, original.SelectedVehicleState,
                new PlayerProfileSaveCheckpoint(original.SaveService), original.ProgressionState, engine.CreateData(), handling.CreateData());
            var garage = new GarageService(original.ProgressionState, engine.CreateData(), handling.CreateData(), () => selection.CurrentSelectedDefinition.BasePerformanceStats);
            var context = new ApplicationContext(_flow, original.ProgressionState, original.SelectedVehicleState, original.SaveService,
                original.VehicleCatalog, selection, garage, new ApexRivals.Settings.Runtime.SettingsService(new MemorySettingsStorage(), new MemoryDisplaySettings()), original.RaceProgressionService, original.PauseCoordinator);
            typeof(GameBootstrapper).GetField("<Context>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_bootstrapper, context);
            _installAllContentScenes = true;
            var input = _bootstrapper.gameObject.AddComponent<BootstrapUiInputSystem>();
            using (var serialized = new SerializedObject(input))
            {
                serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            input.SendMessage("ConfigureInputModule");
        }

        private IEnumerator WaitForStabilityCountdown()
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (Installer.Session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
        }

        private static void CompleteStabilityRace()
        {
            var coordinator = FindInRace<RaceCoordinator>();
            var participant = coordinator.PlayerParticipant;
            for (var i = 0; i < coordinator.TotalLaps * coordinator.OrderedCheckpoints.Count + 1; i++)
            {
                Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
                if (progress.HasFinished) break;
                Assert.That(coordinator.TryPassCheckpoint(participant, coordinator.OrderedCheckpoints[progress.NextExpectedCheckpointIndex]).Accepted, Is.True);
            }
        }

        private object[] TrackStabilityAttempt(List<WeakReference> sessions, List<WeakReference> presenters)
        {
            var ui = FindInRace<RaceUiSceneInstaller>();
            var pause = StabilityField(ui, "_pausePresenter");
            var hud = StabilityField(ui, "_hudPresenter");
            var results = StabilityField(ui, "_resultsPresenter");
            sessions.Add(new WeakReference(Installer.Session));
            foreach (var presenter in new[] { pause, hud, results }) presenters.Add(new WeakReference(presenter));
            return new object[] { Installer.Session, Installer.Session.PlayerVehicleRigidbody, pause, hud, results };
        }

        private static void AssertReleasedStabilityAttempt(object[] attempt)
        {
            Assert.That(((RaceSessionCoordinator)attempt[0]).State, Is.EqualTo(RaceSessionLifecycleState.Disposed));
            Assert.That((Rigidbody)attempt[1] == null, Is.True, "Previous player native object must be destroyed.");
            for (var i = 2; i < attempt.Length; i++) Assert.That(StabilityField(attempt[i], "_view"), Is.Null, "Disposed presenter must release view.");
        }

        private static void AssertSingleStabilityRace()
        {
            Assert.That(SceneManager.GetSceneByName("Race").isLoaded, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<RaceSessionSceneInstaller>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<RaceParticipant>(FindObjectsSortMode.None).Length, Is.EqualTo(2), "Authored roster is player plus one AI.");
            Assert.That(FindInRace<RaceCoordinator>().PlayerParticipant, Is.Not.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        private static IEnumerator CollectStabilityCheckpoint()
        {
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;
        }

        private StabilitySample CaptureStabilitySample(int iteration, double started, double transition,
            List<WeakReference> sessions, List<WeakReference> presenters)
        {
            var session = Installer == null ? null : Installer.Session;
            // Read counters before the diagnostic enumeration/JSON allocations.
            var sample = new StabilitySample {
                iteration = iteration, elapsedSeconds = Time.realtimeSinceStartupAsDouble - started,
                transitionSeconds = transition, managedLiveBytes = GC.GetTotalMemory(false),
                monoHeapBytes = Profiler.GetMonoHeapSizeLong(), unityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                unityReservedBytes = Profiler.GetTotalReservedMemoryLong(), timeScale = Time.timeScale,
                navigationListeners = StabilityListeners(_flow, "StateChanged"),
                sessionStateListeners = StabilityListeners(session, "StateChanged"),
                sessionResultListeners = StabilityListeners(session, "ResultsReady"),
                sessionPositionListeners = StabilityListeners(session, "PositionChanged"),
                retainedSessions = sessions.Count(reference => reference.IsAlive),
                retainedPresenters = presenters.Count(reference => reference.IsAlive)
            };
            sample.objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Length;
            sample.gameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Length;
            sample.materials = Resources.FindObjectsOfTypeAll<Material>().Length;
            sample.meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            sample.textures = Resources.FindObjectsOfTypeAll<Texture>().Length;
            sample.renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
            sample.audioSources = Resources.FindObjectsOfTypeAll<AudioSource>().Length;
            sample.audioClips = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            sample.raceInstallers = UnityEngine.Object.FindObjectsByType<RaceSessionSceneInstaller>(FindObjectsSortMode.None).Length;
            sample.participants = UnityEngine.Object.FindObjectsByType<RaceParticipant>(FindObjectsSortMode.None).Length;
            return sample;
        }

        private static object StabilityField(object target, string name) => target?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
        private static int StabilityListeners(object target, string name) => (StabilityField(target, name) as Delegate)?.GetInvocationList().Length ?? 0;

        private static void AssertStabilityOwnership(StabilitySample sample, bool inRace)
        {
            Assert.That(sample.retainedSessions, Is.Zero, "Old sessions must be collectible at equivalent checkpoints.");
            Assert.That(sample.retainedPresenters, Is.Zero, "Old pause/HUD/results presenters must be collectible.");
            // One application-owned pause coordinator survives content scenes;
            // Race adds exactly one UI listener, removed on every unload.
            Assert.That(sample.navigationListeners, Is.EqualTo(inRace ? 2 : 1));
            Assert.That(sample.sessionStateListeners, Is.EqualTo(inRace ? 2 : 0));
            Assert.That(sample.sessionResultListeners, Is.EqualTo(inRace ? 1 : 0));
            Assert.That(sample.sessionPositionListeners, Is.EqualTo(inRace ? 1 : 0));
        }

        private static IEnumerator CaptureStabilityNativeSnapshot(int iteration, StabilityRun run)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "graphify-out", "phase5-menu-cycle-" + iteration + ".snap"));
            var done = false;
            var succeeded = false;
            Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(path, (filename, success) =>
            {
                succeeded = success;
                done = true;
            }, Unity.Profiling.Memory.CaptureFlags.ManagedObjects | Unity.Profiling.Memory.CaptureFlags.NativeObjects | Unity.Profiling.Memory.CaptureFlags.NativeAllocations);
            var deadline = Time.realtimeSinceStartupAsDouble + 90;
            while (!done && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(done && succeeded && File.Exists(path), Is.True, "Native/managed snapshot capture must finish successfully.");
            run.nativeSnapshots.Add(Path.GetFileName(path));
        }

        private static void WriteStabilityRun(string filename, StabilityRun run)
        {
            var directory = Path.Combine(Application.dataPath, "..", "graphify-out");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, filename), JsonUtility.ToJson(run, true));
        }
    }
}
#endif
