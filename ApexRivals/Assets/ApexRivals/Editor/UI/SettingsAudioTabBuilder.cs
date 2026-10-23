using ApexRivals.Audio.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Editor.UI
{
    /// <summary>
    /// Builds the AUDIO tab of the Settings screen (Master, Music, UI and Vehicles sliders) next to DISPLAY in the
    /// main menu, and adds the <see cref="GameAudio"/> player to the Bootstrap scene. Re-running replaces both.
    /// </summary>
    public static class SettingsAudioTabBuilder
    {
        private const string MainMenuScenePath = "Assets/ApexRivals/Scenes/MainMenu.unity";
        private const string BootstrapScenePath = "Assets/ApexRivals/Scenes/Bootstrap.unity";
        private static readonly Color TrackColor = new Color32(0x47, 0x4C, 0x54, 0xC0);

        private static readonly (string name, string label, string field)[] Rows =
        {
            ("MasterVolume", "MASTER VOLUME", "masterVolumeSlider"),
            ("MusicVolume", "MUSIC", "musicVolumeSlider"),
            ("UiVolume", "UI SOUNDS", "uiVolumeSlider"),
            ("VehicleVolume", "VEHICLES", "vehicleVolumeSlider")
        };

        [MenuItem("ApexRivals/UI/Rebuild Settings Audio Tab")]
        public static void RebuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(Rebuild());
            }
        }

        public static string Rebuild()
        {
            var previous = EditorSceneManager.GetActiveScene().path;
            var report = BuildAudioTab() + " " + AddGameAudio();
            if (!string.IsNullOrEmpty(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }

            return report;
        }

        private static string BuildAudioTab()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<SettingsUguiView>(FindObjectsInactive.Include);
            var panel = view.transform.Find("SettingsPanel");
            var tabs = panel.Find("CategoryNavigation");
            var content = panel.Find("Content");
            var displayTab = tabs.Find("DisplayTab");
            var displayPanel = content.Find("DisplayPanel");
            var sourceLabel = displayPanel.Find("ResolutionRow/ResolutionLabel");
            DestroyChild(tabs, "AudioTab");
            DestroyChild(content, "AudioPanel");

            var audioTab = Object.Instantiate(displayTab.gameObject, tabs, false);
            audioTab.name = "AudioTab";
            var tabText = audioTab.GetComponentInChildren<Text>(true);
            tabText.name = "AudioTabText";
            tabText.text = "AUDIO";

            var audioPanel = new GameObject("AudioPanel", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            audioPanel.layer = displayPanel.gameObject.layer;
            audioPanel.transform.SetParent(content, false);
            audioPanel.transform.SetSiblingIndex(displayPanel.GetSiblingIndex() + 1);
            EditorUtility.CopySerialized(displayPanel.GetComponent<LayoutElement>(), audioPanel.GetComponent<LayoutElement>());
            EditorUtility.CopySerialized(displayPanel.GetComponent<VerticalLayoutGroup>(), audioPanel.GetComponent<VerticalLayoutGroup>());
            // The content column does not control heights, so match the display panel's rect.
            ((RectTransform)audioPanel.transform).sizeDelta = ((RectTransform)displayPanel).sizeDelta;
            SetTabLabel(displayTab);
            SetTabLabel(audioTab.transform);

            var refs = new SerializedObject(view);
            refs.FindProperty("audioTabButton").objectReferenceValue = audioTab.GetComponent<Button>();
            refs.FindProperty("audioPanel").objectReferenceValue = audioPanel;
            var resolutionRow = displayPanel.Find("ResolutionRow");
            foreach (var (name, label, field) in Rows)
            {
                refs.FindProperty(field).objectReferenceValue = BuildRow(audioPanel.transform, resolutionRow, sourceLabel, name, label);
            }

            refs.ApplyModifiedPropertiesWithoutUndo();
            audioPanel.SetActive(false);

            var theme = view.GetComponentInParent<ApexUiTheme>(true);
            if (theme != null)
            {
                theme.ApplyStyles();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Audio tab rebuilt in " + MainMenuScenePath + ".";
        }

        /// <summary>LABEL | slider | 80% row laid out like the display rows.</summary>
        private static Slider BuildRow(Transform parent, Transform sourceRow, Transform sourceLabel, string name, string labelText)
        {
            var row = new GameObject(name + "Row", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            row.layer = parent.gameObject.layer;
            row.transform.SetParent(parent, false);
            EditorUtility.CopySerialized(sourceRow.GetComponent<LayoutElement>(), row.GetComponent<LayoutElement>());
            EditorUtility.CopySerialized(sourceRow.GetComponent<HorizontalLayoutGroup>(), row.GetComponent<HorizontalLayoutGroup>());
            ((RectTransform)row.transform).sizeDelta = ((RectTransform)sourceRow).sizeDelta;

            var label = Object.Instantiate(sourceLabel.gameObject, row.transform, false);
            label.name = name + "Label";
            label.GetComponent<Text>().text = labelText;

            var sliderObject = new GameObject(name + "Slider", typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer), typeof(AngledPanelGraphic), typeof(Slider));
            sliderObject.layer = row.layer;
            sliderObject.transform.SetParent(row.transform, false);
            var layout = sliderObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 520f;
            layout.preferredHeight = 58f;
            // Invisible hit area so a click anywhere along the row moves the slider, not only on the thin track.
            var hitArea = sliderObject.GetComponent<AngledPanelGraphic>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            var track = Panel(sliderObject.transform, "Track", TrackColor);
            Stretch(track.rectTransform, new Vector2(20f, 0f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), 8f);
            var fillArea = Rect(sliderObject.transform, "FillArea");
            Stretch(fillArea, new Vector2(20f, 0f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), 8f);
            var fill = Panel(fillArea, "Fill", ApexUiVisualStyle.PrimaryRed);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect(sliderObject.transform, "HandleSlideArea");
            Stretch(handleArea, new Vector2(20f, 0f), Vector2.zero, Vector2.one, 0f);
            var handle = Panel(handleArea, "Handle", ApexUiVisualStyle.PrimaryText);
            handle.rectTransform.sizeDelta = new Vector2(18f, 0f);
            handle.LeftBottomInset = 4f;
            handle.RightTopInset = 4f;
            handle.raycastTarget = true;

            var slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            var colours = ColorBlock.defaultColorBlock;
            colours.normalColor = Color.white;
            colours.highlightedColor = ApexUiVisualStyle.FocusOutline;
            colours.selectedColor = ApexUiVisualStyle.FocusOutline;
            colours.pressedColor = ApexUiVisualStyle.PrimaryRedPressed;
            colours.disabledColor = ApexUiVisualStyle.Disabled;
            colours.fadeDuration = 0.05f;
            slider.colors = colours;
            slider.transition = Selectable.Transition.ColorTint;

            var value = Object.Instantiate(sourceLabel.gameObject, row.transform, false);
            value.name = name + "Value";
            var valueText = value.GetComponent<Text>();
            valueText.text = "100%";
            valueText.alignment = TextAnchor.MiddleRight;
            var valueLayout = value.GetComponent<LayoutElement>();
            valueLayout.preferredWidth = 90f;
            var percent = value.AddComponent<SliderValueLabel>();
            var percentRefs = new SerializedObject(percent);
            percentRefs.FindProperty("slider").objectReferenceValue = slider;
            percentRefs.FindProperty("label").objectReferenceValue = valueText;
            percentRefs.ApplyModifiedPropertiesWithoutUndo();
            return slider;
        }

        /// <summary>Lets the tab's visual state colour its label, so the open tab reads as current (accent text).</summary>
        private static void SetTabLabel(Transform tab)
        {
            var state = tab.GetComponent<UguiMenuButtonVisualState>();
            var label = tab.GetComponentInChildren<Text>(true);
            if (state == null || label == null)
            {
                return;
            }

            var refs = new SerializedObject(state);
            var labels = refs.FindProperty("labels");
            labels.arraySize = 1;
            labels.GetArrayElementAtIndex(0).objectReferenceValue = label;
            refs.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string AddGameAudio()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            var existing = Object.FindFirstObjectByType<GameAudio>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var audio = new GameObject("GameAudio", typeof(GameAudio));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(audio, scene);
            var refs = new SerializedObject(bootstrapper);
            refs.FindProperty("gameAudio").objectReferenceValue = audio.GetComponent<GameAudio>();
            refs.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "GameAudio added to " + BootstrapScenePath + ".";
        }

        private static AngledPanelGraphic Panel(Transform parent, string name, Color color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(AngledPanelGraphic));
            panelObject.layer = parent.gameObject.layer;
            panelObject.transform.SetParent(parent, false);
            var panel = panelObject.GetComponent<AngledPanelGraphic>();
            panel.color = color;
            panel.raycastTarget = false;
            return panel;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.layer = parent.gameObject.layer;
            rectObject.transform.SetParent(parent, false);
            return (RectTransform)rectObject.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 horizontalInset, Vector2 anchorMin, Vector2 anchorMax, float height)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontalInset.x, -height * 0.5f);
            rect.offsetMax = new Vector2(-horizontalInset.x, height * 0.5f);
            if (anchorMin.y != anchorMax.y)
            {
                rect.offsetMin = new Vector2(horizontalInset.x, 0f);
                rect.offsetMax = new Vector2(-horizontalInset.x, 0f);
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
