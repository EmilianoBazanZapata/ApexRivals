#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApexRivals.RaceSetup.Configuration;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [Serializable]
        private sealed class CounterMeasurement
        {
            public string marker;
            public string unit;
            public bool available;
            public int nonzeroFrames;
            public long sum;
            public long peak;
            public double average;
        }

        [Serializable]
        private sealed class FrameMeasurement
        {
            public string scenario;
            public string environment = "Editor PlayMode; Profiler enabled; neutral player input; no deep profiling";
            public int aiOpponents;
            public bool experimentalRoster;
            public int width;
            public int height;
            public string quality;
            public int vSync;
            public int fpsCap;
            public float fixedDeltaTime;
            public int frames;
            public double durationSeconds;
            public double averageWallFrameMilliseconds;
            public double maxWallFrameMilliseconds;
            public List<CounterMeasurement> counters = new List<CounterMeasurement>();
        }

        [Serializable]
        private sealed class ProfileMeasurements
        {
            public string unity;
            public List<FrameMeasurement> windows = new List<FrameMeasurement>();
        }

        [UnityTest, Category("Phase5Measurement"), Timeout(600000)]
        public IEnumerator MeasureMenusAndExperimentalParticipantCounts()
        {
            ConfigureStabilityMenus();
            var report = new ProfileMeasurements { unity = Application.unityVersion };
            var wasEnabled = Profiler.enabled;
            var originalFocusedWindow = EditorWindow.focusedWindow;
            Profiler.enabled = true;
            try
            {
                var exit = Installer.Session.ReturnToMainMenu();
                yield return WaitForTask(exit);
                Assert.That(exit.Result.Succeeded, Is.True);
                yield return MeasureStabilityFrames("MainMenu idle", 0, 10, report);
                ReadReference<Button>(FindInScene<MainMenuUguiView>("MainMenu"), "settingsButton").onClick.Invoke();
                yield return null;
                yield return MeasureStabilityFrames("Settings idle", 0, 10, report);
                ReadReference<Button>(FindInScene<SettingsUguiView>("MainMenu"), "cancelButton").onClick.Invoke();
                var garage = _flow.OpenGarage();
                yield return WaitForTask(garage);
                Assert.That(garage.Result.Succeeded, Is.True);
                yield return MeasureStabilityFrames("Garage rotating preview", 0, 10, report);
                var start = _flow.StartRace();
                yield return WaitForTask(start);
                Assert.That(start.Result.Succeeded, Is.True);
                yield return WaitForStabilityCountdown();
                yield return MeasureStabilityFrames("Authored Race; neutral player", 1, 30, report);
                var installer = Installer;
                var ui = FindInRace<RaceUiSceneInstaller>();
                var grid = FindInRace<StartingGrid>();
                var authored = ReadReference<RaceSetupDefinition>(installer, "raceSetupDefinition");
                var originalSpawns = new List<Transform>();
                for (var i = 0; i < grid.SpawnPointCount; i++)
                {
                    using var serialized = new SerializedObject(grid);
                    originalSpawns.Add((Transform)serialized.FindProperty("spawnPoints").GetArrayElementAtIndex(i).objectReferenceValue);
                }
                var temporarySpawns = new List<GameObject>();
                var clones = new List<RaceSetupDefinition>();
                try
                {
                    var spawns = new List<Transform>(originalSpawns);
                    while (spawns.Count < 9)
                    {
                        var point = new GameObject("Phase5ExperimentalGrid");
                        point.transform.SetParent(grid.transform, false);
                        point.transform.SetPositionAndRotation(originalSpawns[0].position + originalSpawns[0].rotation * new Vector3((spawns.Count % 2) * 3f, 0, -(spawns.Count / 2 + 1) * 7f), originalSpawns[0].rotation);
                        temporarySpawns.Add(point);
                        spawns.Add(point.transform);
                    }
                    grid.Configure(spawns.ToArray());
                    foreach (var count in new[] { 0, 1, 3, 5, 8 })
                    {
                        ui.Uninstall();
                        installer.Uninstall();
                        yield return null;
                        var clone = UnityEngine.Object.Instantiate(authored);
                        clones.Add(clone);
                        var template = authored.AiOpponents[0];
                        using (var serialized = new SerializedObject(clone))
                        {
                            var opponents = serialized.FindProperty("aiOpponents");
                            opponents.arraySize = count;
                            for (var index = 0; index < count; index++)
                            {
                                var entry = opponents.GetArrayElementAtIndex(index);
                                entry.FindPropertyRelative("participantId").stringValue = "Phase5AI" + index;
                                entry.FindPropertyRelative("vehicleId").stringValue = template.VehicleId;
                                entry.FindPropertyRelative("startingGridIndex").intValue = index + 1;
                                entry.FindPropertyRelative("aiConfigurationId").stringValue = template.AiConfigurationId;
                                entry.FindPropertyRelative("aiConfiguration").objectReferenceValue = template.AiConfiguration;
                            }
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        SetStabilitySetup(installer, clone);
                        installer.Install(_bootstrapper.Context);
                        Assert.That(installer.LastInstallResult.Succeeded, Is.True, installer.LastInstallResult.Message);
                        ui.Install(_bootstrapper.Context);
                        yield return WaitForStabilityCountdown();
                        Assert.That(UnityEngine.Object.FindObjectsByType<ApexRivals.Race.Runtime.RaceParticipant>(FindObjectsSortMode.None).Length, Is.EqualTo(count + 1));
                        yield return MeasureStabilityFrames("Experimental Race; neutral player; runtime-only roster/grid", count, 10, report, true);
                    }
                }
                finally
                {
                    ui.Uninstall();
                    installer.Uninstall();
                    SetStabilitySetup(installer, authored);
                    grid.Configure(originalSpawns.ToArray());
                    foreach (var point in temporarySpawns) UnityEngine.Object.Destroy(point);
                    foreach (var clone in clones) UnityEngine.Object.Destroy(clone);
                }
            }
            finally
            {
                Profiler.enabled = wasEnabled;
                if (originalFocusedWindow != null) originalFocusedWindow.Focus();
                WriteProfileMeasurements(report);
            }
        }

        private static void SetStabilitySetup(UnityEngine.Object installer, RaceSetupDefinition setup)
        {
            using var serialized = new SerializedObject(installer);
            serialized.FindProperty("raceSetupDefinition").objectReferenceValue = setup;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private IEnumerator MeasureStabilityFrames(string scenario, int ai, float seconds, ProfileMeasurements report, bool experimental = false)
        {
            yield return new WaitForSecondsRealtime(1f);
            var names = new[] { "Main Thread", "GC Allocated In Frame", "Physics.Simulate", "FixedUpdate.ScriptRunBehaviourFixedUpdate",
                "Canvas.BuildBatch", "Draw Calls Count", "Batches Count", "SetPass Calls Count",
                "ApexRivals.Runtime.dll!ApexRivals.Vehicle.Runtime::WheelArcadeVehicleController.FixedUpdate() [Invoke]",
                "ApexRivals.Runtime.dll!ApexRivals.AI.Runtime::AiDrivingInputProvider.Update() [Invoke]",
                "ApexRivals.Runtime.dll!ApexRivals.Race.Runtime::RaceCoordinator.Update() [Invoke]",
                "ApexRivals.Runtime.dll!ApexRivals.Camera.Runtime::WheelVehicleFollowCamera.LateUpdate() [Invoke]" };
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions = handles.Select(ProfilerRecorderHandle.GetDescription).ToArray();
            var measurement = new FrameMeasurement { scenario = scenario, aiOpponents = ai, experimentalRoster = experimental,
                width = Screen.width, height = Screen.height, quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                vSync = QualitySettings.vSyncCount, fpsCap = Application.targetFrameRate, fixedDeltaTime = Time.fixedDeltaTime };
            var recorders = new ProfilerRecorder[names.Length];
            try
            {
                for (var i = 0; i < names.Length; i++)
                {
                    var matching = descriptions.Where(d => d.Name == names[i]).ToArray();
                    var counter = new CounterMeasurement { marker = names[i] };
                    if (matching.Length > 0)
                    {
                        recorders[i] = ProfilerRecorder.StartNew(matching[0].Category, names[i], 1);
                        counter.available = recorders[i].Valid;
                        counter.unit = matching[0].UnitType.ToString();
                    }
                    measurement.counters.Add(counter);
                }
                var started = Time.realtimeSinceStartupAsDouble;
                var previous = started;
                double wallTotal = 0;
                while (Time.realtimeSinceStartupAsDouble - started < seconds)
                {
                    yield return null;
                    var now = Time.realtimeSinceStartupAsDouble;
                    var wall = (now - previous) * 1000;
                    previous = now;
                    wallTotal += wall;
                    measurement.maxWallFrameMilliseconds = Math.Max(measurement.maxWallFrameMilliseconds, wall);
                    measurement.frames++;
                    for (var i = 0; i < recorders.Length; i++)
                    {
                        if (!recorders[i].Valid) continue;
                        var value = recorders[i].LastValue;
                        var counter = measurement.counters[i];
                        counter.sum += value;
                        counter.peak = Math.Max(counter.peak, value);
                        if (value != 0) counter.nonzeroFrames++;
                    }
                }
                measurement.durationSeconds = Time.realtimeSinceStartupAsDouble - started;
                measurement.averageWallFrameMilliseconds = wallTotal / measurement.frames;
                foreach (var counter in measurement.counters) counter.average = (double)counter.sum / measurement.frames;
                report.windows.Add(measurement);
                WriteProfileMeasurements(report);
            }
            finally { foreach (var recorder in recorders) recorder.Dispose(); }
        }

        private static void WriteProfileMeasurements(ProfileMeasurements report)
        {
            var directory = Path.Combine(Application.dataPath, "..", "graphify-out");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "phase5-frame-measurements.json"), JsonUtility.ToJson(report, true));
        }
    }
}
#endif
