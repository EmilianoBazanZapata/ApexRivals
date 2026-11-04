using System.Text;
using ApexRivals.Audio.Runtime;
using ApexRivals.Environment.Runtime;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ApexRivals.Editor.Audio
{
    /// <summary>
    /// Puts every game sound on an AudioSource set up in a prefab or scene and routed through its
    /// <see cref="AudioBus"/>: bakes missing generated clips, sets up the cars, routes the Bootstrap music/UI player,
    /// the trackside ambience, the weather and the start lights. Reports the real-voice budget without changing
    /// Project Settings. Safe to re-run: it only fills in what is missing and preserves Inspector choices.
    /// </summary>
    public static class GameAudioSetup
    {
        private const string BootstrapScenePath = "Assets/ApexRivals/Scenes/Bootstrap.unity";
        private const string StartLightsPrefabPath = "Assets/ApexRivals/UI/StartLights/RaceStartLights.prefab";
        private const int RealVoices = 64;
        private static readonly string[] RaceScenes = { "Race", "RaceDunes", "RaceSuzuka", "RaceRiverdale" };

        [MenuItem("ApexRivals/Audio/Set Up Audio Sources")]
        public static void RunFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(Run());
            }
        }

        public static string Run()
        {
            var report = new StringBuilder();
            report.AppendLine(AudioClipBaker.Bake());
            report.Append(VehicleEngineSoundAssigner.Assign());
            report.AppendLine(RouteGameAudio());
            report.Append(RouteAmbience());
            report.AppendLine(RouteStartLights());
            report.AppendLine(ReportVoiceCount());
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>
        /// The Bootstrap music and UI cue sources: routed through their buses and the mixer, and the mixer assigned to
        /// <see cref="GameAudio"/>, wherever that is still missing.
        /// </summary>
        private static string RouteGameAudio()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var audio = Object.FindFirstObjectByType<GameAudio>(FindObjectsInactive.Include);
            if (audio == null) return "Bootstrap: GameAudio not found";
            var refs = new SerializedObject(audio);
            var mixer = refs.FindProperty("mixer");
            if (mixer.objectReferenceValue == null)
            {
                mixer.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(AudioMixerBuilder.MixerPath);
                refs.ApplyModifiedPropertiesWithoutUndo();
            }

            var music = refs.FindProperty("music").objectReferenceValue as AudioBusSource;
            var cues = refs.FindProperty("uiCues").objectReferenceValue as AudioBusSource;
            if (music != null) AudioSourcePresets.Route(music.Source, AudioBus.Music, 0.45f, false);
            if (cues != null) AudioSourcePresets.Route(cues.Source, AudioBus.Ui, 0.7f, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Bootstrap: music and UI cues routed";
        }

        /// <summary>Crowd and weather beds (atmosphere prefabs) and the rain/wind of each race scene: Ambience bus.</summary>
        private static string RouteAmbience()
        {
            var report = new StringBuilder();
            foreach (var sceneName in RaceScenes)
            {
                var prefabPath = "Assets/ApexRivals/Environment/Prefabs/" + sceneName + "Atmosphere.prefab";
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var atmosphere = root.GetComponent<TrackAtmosphere>();
                    if (atmosphere == null)
                    {
                        report.AppendLine(prefabPath + ": no TrackAtmosphere");
                    }
                    else
                    {
                        foreach (var source in atmosphere.CrowdSources) RouteAmbience(source);
                        RouteAmbience(atmosphere.WeatherSource);
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                var weathers = Object.FindObjectsByType<RaceWeatherController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var weather in weathers)
                {
                    RouteAmbience(weather.RainAudio);
                    RouteAmbience(weather.WindAudio);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.AppendLine(sceneName + ": ambience routed (" + weathers.Length + " weather controller(s))");
            }

            return report.ToString();
        }

        private static void RouteAmbience(AudioSource source)
        {
            if (source != null) AudioSourcePresets.Route(source, AudioBus.Ambience, 1f, false);
        }

        private static string RouteStartLights()
        {
            var root = PrefabUtility.LoadPrefabContents(StartLightsPrefabPath);
            try
            {
                var lights = root.GetComponent<RaceStartLights>();
                if (lights == null || lights.AudioSource == null) return "Start lights: no audio source";
                AudioSourcePresets.Route(lights.AudioSource, AudioBus.Ui, 1f, false);
                PrefabUtility.SaveAsPrefabAsset(root, StartLightsPrefabPath);
                return "Start lights: routed through the UI bus";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Eight cars with an engine, a skid and the odd hit, plus ambience and UI, need more than 32 voices.</summary>
        private static string ReportVoiceCount()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (settings.Length == 0) return "AudioManager settings not found";
            var serialized = new SerializedObject(settings[0]);
            var real = serialized.FindProperty("m_RealVoiceCount");
            if (real == null) return "AudioManager: m_RealVoiceCount not found";
            return "Real voices: " + real.intValue + (real.intValue < RealVoices
                ? ". Consider at least " + RealVoices + " in Project Settings > Audio for a full grid; kept the configured budget."
                : ". Kept the configured budget.");
        }
    }
}
