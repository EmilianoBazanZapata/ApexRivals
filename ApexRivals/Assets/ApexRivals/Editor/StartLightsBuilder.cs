using System;
using System.IO;
using System.Linq;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ApexRivals.Editor
{
    /// <summary>Authors ordinary editable assets; never runs automatically on import or in a build.</summary>
    public static class StartLightsBuilder
    {
        public const string Folder = "Assets/ApexRivals/UI/StartLights";
        public const string PrefabPath = Folder + "/RaceStartLights.prefab";

        [MenuItem("Apex Rivals/UI/Rebuild Start Lights")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes before rebuilding start lights.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
                var controller = BuildController();
                WriteCue("Countdown", 660, 0.14f);
                WriteCue("Go", 1320, 0.4f);
                var root = new GameObject("RaceStartLights", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
                try
                {
                    var canvas = root.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 200;
                    canvas.enabled = false;
                    var scaler = root.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    root.GetComponent<CanvasGroup>().blocksRaycasts = false;
                    var panel = Rect("Panel", root.transform, new Vector2(520, 230), new Vector2(0, -160));
                    panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
                    panel.gameObject.AddComponent<Image>().color = ApexUiVisualStyle.Panel;
                    var trim = Rect("Accent", panel, new Vector2(520, 4), new Vector2(0, 113));
                    trim.gameObject.AddComponent<Image>().color = ApexUiVisualStyle.PrimaryRed;
                    for (var i = 0; i < 3; i++)
                    {
                        var lamp = Rect("Lamp" + i, panel, new Vector2(110, 110), new Vector2((i - 1) * 150, 32));
                        lamp.gameObject.AddComponent<OctagonalStartLightGraphic>().color = Off;
                    }
                    var text = Rect("Label", panel, new Vector2(450, 100), new Vector2(0, -66)).gameObject.AddComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 78;
                    text.fontStyle = FontStyle.Bold;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = ApexUiVisualStyle.PrimaryText;
                    text.text = "3";
                    var animator = root.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.updateMode = AnimatorUpdateMode.Normal;
                    animator.enabled = false;
                    var source = root.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0;
                    source.ignoreListenerPause = false;
                    var view = root.AddComponent<RaceStartLights>();
                    var serialized = new SerializedObject(view);
                    Reference(serialized, "display", canvas);
                    Reference(serialized, "animator", animator);
                    Reference(serialized, "label", text);
                    Reference(serialized, "audioSource", source);
                    Reference(serialized, "countdownCue", AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Countdown.wav"));
                    Reference(serialized, "goCue", AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Go.wav"));
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { Object.DestroyImmediate(root); }

                foreach (var sceneName in new[] { "Race", "RaceDunes", "RaceSuzuka" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + sceneName + ".unity");
                    var existing = Find<RaceStartLights>(scene);
                    if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                    Undo.RegisterCreatedObjectUndo(instance, "Install start lights");
                    var serialized = new SerializedObject(instance.GetComponent<RaceStartLights>());
                    Reference(serialized, "coordinator", Find<RaceCoordinator>(scene));
                    Reference(serialized, "sessionInstaller", Find<RaceSessionSceneInstaller>(scene));
                    var hud = Find<RaceHudUguiView>(scene);
                    if (hud == null) throw new InvalidOperationException("Missing race HUD in " + sceneName);
                    Reference(serialized, "legacyCountdown", new SerializedObject(hud).FindProperty("countdownText").objectReferenceValue);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<RaceStartLights>());
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static readonly Color Off = new Color32(31, 38, 43, 255);
        private static readonly Color Red = new Color32(255, 40, 30, 255);
        private static readonly Color Green = new Color32(55, 255, 112, 255);

        private static AnimatorController BuildController()
        {
            var path = Folder + "/StartLights.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            foreach (var name in new[] { "Idle", "Three", "Two", "One", "Go" })
            {
                var clipPath = Folder + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null) { clip = new AnimationClip { name = name, frameRate = 60 }; AssetDatabase.CreateAsset(clip, clipPath); }
                clip.ClearCurves();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                var duration = name == "Go" ? 1.05f : 1f;
                var count = name == "Three" ? 1 : name == "Two" ? 2 : name == "One" || name == "Go" ? 3 : 0;
                for (var i = 0; i < 3; i++)
                    ColorCurves(clip, "Panel/Lamp" + i, typeof(OctagonalStartLightGraphic), i < count ? name == "Go" ? Green : Red : Off, duration);
                ColorCurves(clip, "Panel/Label", typeof(Text), name == "Go" ? Green : ApexUiVisualStyle.PrimaryText, duration);
                Curve(clip, "", typeof(CanvasGroup), "m_Alpha", name == "Go"
                    ? new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.65f, 1), new Keyframe(duration, 0))
                    : AnimationCurve.Constant(0, duration, name == "Idle" ? 0 : 1));
                foreach (var axis in new[] { "x", "y", "z" })
                    Curve(clip, "Panel", typeof(RectTransform), "m_LocalScale." + axis,
                        new AnimationCurve(new Keyframe(0, name == "Idle" ? 1 : 1.1f), new Keyframe(0.18f, 1), new Keyframe(duration, 1)));
                var state = machine.AddState(name, new Vector3(300, count * 70 + (name == "Go" ? 70 : 0)));
                state.motion = clip;
                state.writeDefaultValues = false;
                if (name == "Idle") machine.defaultState = state;
                EditorUtility.SetDirty(clip);
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ColorCurves(AnimationClip clip, string path, Type type, Color color, float duration)
        {
            var channels = new[] { "r", "g", "b", "a" };
            for (var i = 0; i < 4; i++) Curve(clip, path, type, "m_Color." + channels[i], AnimationCurve.Constant(0, duration, color[i]));
        }
        private static void Curve(AnimationClip clip, string path, Type type, string property, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);

        // Original synthesized cues, baked to editable PCM assets. No runtime clip generation.
        private static void WriteCue(string name, float frequency, float duration)
        {
            const int rate = 44100;
            var samples = Mathf.RoundToInt(rate * duration);
            var path = Folder + "/" + name + ".wav";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (var i = 0; i < samples; i++)
                {
                    var t = (float)i / rate;
                    var envelope = Mathf.Min(1, t / 0.008f) * Mathf.Min(1, (duration - t) / 0.035f);
                    var tone = Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.7f + Mathf.Sin(4 * Mathf.PI * frequency * t) * 0.12f;
                    writer.Write((short)(tone * envelope * short.MaxValue * 0.7f));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static void Reference(SerializedObject target, string name, Object value) => target.FindProperty(name).objectReferenceValue = value;
    }
}
