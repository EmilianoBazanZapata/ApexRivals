using System.Text;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.VFX.Runtime;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Editor.UI
{
    /// <summary>
    /// Authors the pieces of UI that used to be assembled in code at runtime, so they are edited in the editor:
    /// <list type="bullet">
    /// <item>SceneLoadingOverlay prefab, placed under the game bootstrapper in Bootstrap.</item>
    /// <item>RaceIntroductionHint prefab and the race introduction's Cinemachine rig (four shot cameras) under each
    /// race scene's PreRacePresentation, plus the (disabled) CinemachineBrain on the chase camera.</item>
    /// <item>VictoryConfetti prefab, assigned to the RaceVfxDefinition.</item>
    /// <item>The transparent click blocker under the pause menu's confirmation dialog.</item>
    /// </list>
    /// The prefabs are only generated when missing; edit them freely afterwards. Re-running fills in whatever a
    /// scene is missing and leaves existing objects alone.
    /// </summary>
    public static class RuntimeUiPrefabsInstaller
    {
        private const string UiPrefabFolder = "Assets/ApexRivals/UI/Prefabs";
        private const string VfxPrefabFolder = "Assets/ApexRivals/VFX/Prefabs";
        public const string LoadingOverlayPath = UiPrefabFolder + "/SceneLoadingOverlay.prefab";
        public const string IntroductionHintPath = UiPrefabFolder + "/RaceIntroductionHint.prefab";
        public const string ConfettiPath = VfxPrefabFolder + "/VictoryConfetti.prefab";
        private const string VfxDefinitionPath = "Assets/ApexRivals/Content/Configuration/VFX/RaceVfxDefinition.asset";
        private const string ScenesPath = "Assets/ApexRivals/Scenes/";
        private static readonly string[] RaceScenes = { "Race", "RaceDunes", "RaceSuzuka", "RaceRiverdale" };
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        [MenuItem("ApexRivals/UI/Install Authored Runtime UI")]
        public static void InstallFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(Install());
            }
        }

        public static string Install()
        {
            var report = new StringBuilder();
            EnsureFolder("Assets/ApexRivals/UI", "Prefabs");
            EnsureFolder("Assets/ApexRivals/VFX", "Prefabs");
            var overlay = LoadOrBuild(LoadingOverlayPath, BuildLoadingOverlay);
            var hint = LoadOrBuild(IntroductionHintPath, BuildIntroductionHint);
            var confetti = LoadOrBuild(ConfettiPath, BuildConfetti);

            var definition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(VfxDefinitionPath);
            if (definition != null && definition.confettiPrefab == null)
            {
                definition.confettiPrefab = confetti;
                EditorUtility.SetDirty(definition);
                report.AppendLine("RaceVfxDefinition: confetti prefab assigned");
            }

            report.AppendLine(InScene("Bootstrap", scene => InstallLoadingOverlay(overlay)));
            foreach (var name in RaceScenes)
            {
                report.AppendLine(InScene(name, scene => InstallIntroduction(hint) + "; " + InstallModalBlocker()));
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenesPath + "Bootstrap.unity", OpenSceneMode.Single);
            return report.ToString();
        }

        // ---------------------------------------------------------------- scenes

        private static string InstallLoadingOverlay(GameObject prefab)
        {
            var bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null) return "GameBootstrapper not found";
            var loading = bootstrapper.GetComponent<SceneLoadingOverlay>();
            if (loading == null) loading = bootstrapper.gameObject.AddComponent<SceneLoadingOverlay>();
            var refs = new SerializedObject(loading);
            var screen = refs.FindProperty("surface").objectReferenceValue as GameObject;
            if (screen == null)
            {
                screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bootstrapper.transform);
                screen.SetActive(false);
                refs.FindProperty("surface").objectReferenceValue = screen;
            }
            if (refs.FindProperty("spinner").objectReferenceValue == null)
                refs.FindProperty("spinner").objectReferenceValue = screen.transform.Find("LoadingSpinner");
            refs.ApplyModifiedPropertiesWithoutUndo();
            return "loading overlay placed";
        }

        private static string InstallIntroduction(GameObject hintPrefab)
        {
            var presentation = Object.FindFirstObjectByType<PreRacePresentation>(FindObjectsInactive.Include);
            if (presentation == null) return "PreRacePresentation not found";
            var refs = new SerializedObject(presentation);
            var report = new StringBuilder();

            if (refs.FindProperty("introductionHint").objectReferenceValue == null)
            {
                var hint = (GameObject)PrefabUtility.InstantiatePrefab(hintPrefab, presentation.transform);
                hint.SetActive(false);
                refs.FindProperty("introductionHint").objectReferenceValue = hint;
                report.Append("hint placed, ");
            }

            var follow = refs.FindProperty("followCamera").objectReferenceValue as WheelVehicleFollowCamera;
            var output = follow != null ? follow.GetComponent<UnityEngine.Camera>() : null;
            if (output == null)
            {
                refs.ApplyModifiedPropertiesWithoutUndo();
                return report + "no chase camera";
            }

            var rig = refs.FindProperty("shotRig").objectReferenceValue as GameObject;
            var shots = new[] { "gridShot", "rivalsShot", "playerShot", "handoffShot" };
            if (rig == null)
            {
                foreach (var slot in shots)
                    if (refs.FindProperty(slot).objectReferenceValue != null)
                    {
                        refs.ApplyModifiedPropertiesWithoutUndo();
                        return report + "custom shots preserved; assign their shotRig before installing missing shots";
                    }
                rig = new GameObject("PreRaceCinemachineRig");
                rig.transform.SetParent(presentation.transform, false);
                rig.SetActive(false);
                refs.FindProperty("shotRig").objectReferenceValue = rig;
                report.Append("shot rig built, ");
            }
            var names = new[] { "Grid", "Rivals", "Player", "DrivingHandoff" };
            var fovs = new[] { 55f, 52f, 52f, output.fieldOfView };
            var near = Mathf.Min(output.nearClipPlane, 0.15f);
            for (var i = 0; i < shots.Length; i++)
                if (refs.FindProperty(shots[i]).objectReferenceValue == null)
                {
                    refs.FindProperty(shots[i]).objectReferenceValue = Shot(rig.transform, names[i], output, fovs[i], near);
                    report.Append(names[i] + " shot added, ");
                }

            refs.ApplyModifiedPropertiesWithoutUndo();

            var brain = output.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = output.gameObject.AddComponent<CinemachineBrain>();
                // Off until the introduction takes the camera; driven manually so it also runs paused.
                brain.enabled = false;
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.IgnoreTimeScale = true;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, refs.FindProperty("blendSeconds").floatValue);
                report.Append("brain added");
            }

            return report.Length == 0 ? "introduction already set up" : report.ToString();
        }

        private static CinemachineCamera Shot(Transform rig, string name, UnityEngine.Camera output, float fieldOfView, float near)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            go.SetActive(false);
            var shot = go.AddComponent<CinemachineCamera>();
            shot.Priority = 100;
            var lens = LensSettings.FromCamera(output);
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = near;
            shot.Lens = lens;
            return shot;
        }

        private static string InstallModalBlocker()
        {
            var pause = Object.FindFirstObjectByType<PauseUguiView>(FindObjectsInactive.Include);
            if (pause == null) return "PauseUguiView not found";
            var dialog = new SerializedObject(pause).FindProperty("confirmationDialogRoot").objectReferenceValue as GameObject;
            if (dialog == null) return "no confirmation dialog";
            if (dialog.transform.Find("ModalPointerBlocker") != null) return "click blocker already set up";

            // A transparent raycast target covering the screen, drawn under the dialog content: clicks outside the
            // dialog box are swallowed instead of reaching the controls behind it.
            var blocker = new GameObject("ModalPointerBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)blocker.transform;
            rect.SetParent(dialog.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100000f, 100000f);
            rect.anchoredPosition = Vector2.zero;
            var image = blocker.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            return "click blocker placed";
        }

        // ---------------------------------------------------------------- prefabs

        private static GameObject BuildLoadingOverlay()
        {
            var root = Canvas("SceneLoadingOverlay", short.MaxValue, true);
            root.AddComponent<GraphicRaycaster>();
            var background = Child(root.transform, "OpaqueBackground", typeof(Image));
            Stretch(background);
            background.GetComponent<Image>().color = ApexUiVisualStyle.Background;

            // MainMenu wordmark treatment: bold italic, white/red.
            Label(root.transform, "APEX", new Vector2(0f, 100f), new Vector2(650f, 140f), ApexUiVisualStyle.LogoPrimarySize, ApexUiVisualStyle.PrimaryText);
            Label(root.transform, "RIVALS", Vector2.zero, new Vector2(650f, 70f), ApexUiVisualStyle.LogoAccentSize, ApexUiVisualStyle.PrimaryRed);

            var spinner = (RectTransform)Child(root.transform, "LoadingSpinner").transform;
            spinner.anchoredPosition = new Vector2(0f, -150f);
            spinner.sizeDelta = new Vector2(64f, 64f);
            for (var index = 0; index < 8; index++)
            {
                var dot = (RectTransform)Child(spinner, "SpinnerSegment", typeof(Image)).transform;
                var angle = index * Mathf.PI / 4f;
                dot.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 25f;
                dot.sizeDelta = new Vector2(7f, 12f);
                dot.localRotation = Quaternion.Euler(0f, 0f, -index * 45f);
                var image = dot.GetComponent<Image>();
                var color = ApexUiVisualStyle.PrimaryRed;
                color.a = 0.2f + index * 0.1f;
                image.color = color;
                image.raycastTarget = false;
            }

            return root;
        }

        private static GameObject BuildIntroductionHint()
        {
            var root = Canvas("RaceIntroductionHint", 200, false);
            var label = Child(root.transform, "SkipHint", typeof(Text));
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 40f);
            rect.sizeDelta = new Vector2(600f, 50f);
            var text = label.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = "ENTER / SPACE / A  ·  SKIP INTRO";
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleRight;
            text.color = ApexUiVisualStyle.PrimaryText;
            text.raycastTarget = false;
            return root;
        }

        private static GameObject BuildConfetti()
        {
            // Above the results; below the loading overlay (32767).
            var root = Canvas("VictoryConfetti", 1000, false);
            var papers = Child(root.transform, "LowPolyPapers");
            Stretch(papers);
            var confetti = papers.AddComponent<VictoryConfettiGraphic>();
            var definition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(VfxDefinitionPath);
            confetti.material = definition != null ? definition.confettiMaterial : null;
            confetti.raycastTarget = false;
            return root;
        }

        // ---------------------------------------------------------------- helpers

        private static string InScene(string name, System.Func<UnityEngine.SceneManagement.Scene, string> install)
        {
            var scene = EditorSceneManager.OpenScene(ScenesPath + name + ".unity", OpenSceneMode.Single);
            var message = install(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return name + ": " + message;
        }

        private static GameObject LoadOrBuild(string path, System.Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = build();
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject Canvas(string name, int sortingOrder, bool expand)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            if (expand) scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return root;
        }

        private static GameObject Child(Transform parent, string name, params System.Type[] components)
        {
            var types = new System.Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            components.CopyTo(types, 1);
            var child = new GameObject(name, types);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Stretch(GameObject target)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var label = Child(parent, value, typeof(Text));
            var rect = (RectTransform)label.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = label.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
        }

        private static Font BuiltinFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
