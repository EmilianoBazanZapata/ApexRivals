using System.Text;
using ApexRivals.AI.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Editor.UI
{
    /// <summary>
    /// Adds the gear readout (under the speed, bottom right) and the minimap with car markers (bottom left) to the race HUD of every
    /// race scene, and wires them to <see cref="RaceHudUguiView"/> and <see cref="RaceUiSceneInstaller"/>.
    /// Re-running replaces both panels.
    /// </summary>
    public static class RaceHudExtrasBuilder
    {
        private static readonly string[] RaceScenes =
        {
            "Assets/ApexRivals/Scenes/Race.unity",
            "Assets/ApexRivals/Scenes/RaceDunes.unity",
            "Assets/ApexRivals/Scenes/RaceSuzuka.unity"
        };

        private const float MinimapSize = 280f;
        private const float MinimapMargin = 40f;
        private const float MinimapInset = 18f;
        private const float RivalMarkerSize = 12f;
        private const float PlayerMarkerSize = 20f;
        private const float PlayerMarkerDotSize = 13f;

        [MenuItem("ApexRivals/UI/Rebuild Race HUD Gear and Minimap")]
        public static void RebuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(RebuildAll());
            }
        }

        public static string RebuildAll()
        {
            var previous = EditorSceneManager.GetActiveScene().path;
            var report = new StringBuilder();
            foreach (var path in RaceScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                report.AppendLine(path + ": " + Rebuild());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }

            return report.ToString();
        }

        private static string Rebuild()
        {
            var hud = Object.FindFirstObjectByType<RaceHudUguiView>(FindObjectsInactive.Include);
            var installer = Object.FindFirstObjectByType<RaceUiSceneInstaller>(FindObjectsInactive.Include);
            if (hud == null || installer == null)
            {
                return "no race HUD in this scene";
            }

            var panel = hud.transform;
            DestroyChild(panel, "GearPanel");
            DestroyChild(panel, "MinimapPanel");

            var gearValue = BuildGearColumn(panel.parent.Find("VehicleTelemetryPanel"));
            var minimap = BuildMinimap(panel);

            var hudRefs = new SerializedObject(hud);
            hudRefs.FindProperty("gearValueText").objectReferenceValue = gearValue;
            hudRefs.ApplyModifiedPropertiesWithoutUndo();

            var installerRefs = new SerializedObject(installer);
            installerRefs.FindProperty("minimapView").objectReferenceValue = minimap;
            installerRefs.FindProperty("raceCoordinator").objectReferenceValue = Object.FindFirstObjectByType<RaceCoordinator>(FindObjectsInactive.Include);
            installerRefs.FindProperty("racingLine").objectReferenceValue = Object.FindFirstObjectByType<RacingLine>(FindObjectsInactive.Include);
            installerRefs.ApplyModifiedPropertiesWithoutUndo();

            var theme = hud.GetComponentInParent<ApexUiTheme>(true);
            if (theme != null)
            {
                theme.ApplyStyles();
            }

            return "gear and minimap panels rebuilt";
        }

        /// <summary>
        /// Appends GEAR + value under the speed in the vehicle telemetry panel (bottom right). The panel's vertical
        /// layout stacks them and its size fitter grows it; the clones keep the speed texts' theme roles.
        /// </summary>
        private static Text BuildGearColumn(Transform telemetryPanel)
        {
            DestroyChild(telemetryPanel, "GearLabel");
            DestroyChild(telemetryPanel, "GearValue");
            Clone(telemetryPanel.Find("SpeedLabel"), "GearLabel", "GEAR").transform.SetAsLastSibling();
            var value = Clone(telemetryPanel.Find("SpeedValue"), "GearValue", "N");
            value.transform.SetAsLastSibling();
            return value;
        }

        private static Text Clone(Transform source, string name, string text)
        {
            var clone = Object.Instantiate(source.gameObject, source.parent, false);
            clone.name = name;
            var label = clone.GetComponent<Text>();
            label.text = text;
            return label;
        }

        private static RaceMinimapUguiView BuildMinimap(Transform hudPanel)
        {
            var panelObject = new GameObject("MinimapPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(AngledPanelGraphic), typeof(RaceMinimapUguiView));
            panelObject.layer = hudPanel.gameObject.layer;
            panelObject.transform.SetParent(hudPanel, false);
            var rect = (RectTransform)panelObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(MinimapMargin, MinimapMargin);
            rect.sizeDelta = new Vector2(MinimapSize, MinimapSize);
            var background = panelObject.GetComponent<AngledPanelGraphic>();
            background.color = ApexUiVisualStyle.Panel;
            background.BorderThickness = ApexUiVisualStyle.PanelBorderThickness;
            background.BorderColor = ApexUiVisualStyle.PanelOutline;
            background.RightTopInset = 14f;
            background.raycastTarget = false;

            var trackObject = new GameObject("MinimapTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(TrackMinimapGraphic));
            trackObject.layer = panelObject.layer;
            trackObject.transform.SetParent(panelObject.transform, false);
            var trackRect = (RectTransform)trackObject.transform;
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.offsetMin = new Vector2(MinimapInset, MinimapInset);
            trackRect.offsetMax = new Vector2(-MinimapInset, -MinimapInset);
            var track = trackObject.GetComponent<TrackMinimapGraphic>();
            track.color = ApexUiVisualStyle.SecondaryText;
            track.MarkerColor = ApexUiVisualStyle.PrimaryText;
            track.MarkerAlternateColor = ApexUiVisualStyle.PrimaryRed;
            track.raycastTarget = false;

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var rival = Marker(trackObject.transform, "RivalMarkerTemplate", knob, RivalMarkerSize, ApexUiVisualStyle.PrimaryText);
            // Player: white ring around a red dot, larger than the rivals.
            var player = Marker(trackObject.transform, "PlayerMarkerTemplate", knob, PlayerMarkerSize, ApexUiVisualStyle.FocusedText);
            Marker(player, "PlayerMarkerDot", knob, PlayerMarkerDotSize, ApexUiVisualStyle.PrimaryRed).gameObject.SetActive(true);
            rival.gameObject.SetActive(false);
            player.gameObject.SetActive(false);

            var view = panelObject.GetComponent<RaceMinimapUguiView>();
            var refs = new SerializedObject(view);
            refs.FindProperty("track").objectReferenceValue = track;
            refs.FindProperty("rivalMarkerTemplate").objectReferenceValue = rival;
            refs.FindProperty("playerMarkerTemplate").objectReferenceValue = player;
            refs.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static RectTransform Marker(Transform parent, string name, Sprite sprite, float size, Color color)
        {
            var marker = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            marker.layer = parent.gameObject.layer;
            marker.transform.SetParent(parent, false);
            var rect = (RectTransform)marker.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = marker.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
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
