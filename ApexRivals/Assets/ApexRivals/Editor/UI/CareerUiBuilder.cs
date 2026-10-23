using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ApexRivals.Editor.UI
{
    /// <summary>
    /// (Re)builds the career widgets on the existing screens and wires them to their views: the career panel
    /// on the main menu and garage, the car status line and Buy button in the garage, the requirement and
    /// objective lines on Race Select, and the career block on every race scene's results panel.
    /// Running it again replaces what it built. Layout is in 1920x1080 reference pixels from the top-left.
    /// </summary>
    public static class CareerUiBuilder
    {
        private const string ScenesPath = "Assets/ApexRivals/Scenes/";
        private static readonly string[] RaceScenes = { "Race", "RaceDunes", "RaceSuzuka" };

        [MenuItem("ApexRivals/UI/Rebuild Career UI")]
        public static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Debug.Log(Rebuild());
        }

        public static string Rebuild()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine(InScene("MainMenu", BuildMainMenu));
            report.AppendLine(InScene("Garage", BuildGarage));
            foreach (var race in RaceScenes)
            {
                report.AppendLine(InScene(race, BuildResults));
            }

            return report.ToString();
        }

        private static string InScene(string sceneName, System.Func<Scene, string> build)
        {
            var path = ScenesPath + sceneName + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                var message = build(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return sceneName + ": " + message;
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static string BuildMainMenu(Scene scene)
        {
            var view = Find<MainMenuUguiView>(scene);
            if (view == null) return "MainMenuUguiView not found.";
            var mainPanel = view.transform.Find("MainPanel");
            var panel = BuildCareerPanel(mainPanel, "CareerPanel", TopRight(64f, 64f, 640f, 300f), true);

            Assign(view, "careerPanel", panel);
            Theme(view);
            return "career panel built.";
        }

        private static string BuildGarage(Scene scene)
        {
            var view = Find<GarageUguiView>(scene);
            if (view == null) return "GarageUguiView not found.";
            var canvas = view.transform;

            var panel = BuildCareerPanel(canvas, "CareerPanel", TopRight(56f, 184f, 560f, 236f), false);

            Remove(canvas, "VehicleOwnershipStatus");
            var status = Text(canvas, "VehicleOwnershipStatus", string.Empty, TextAnchor.MiddleCenter, TopCenter(156f, 980f, 30f));
            Role(status, ApexUiTextRoleKind.AccentLabel);

            Remove(canvas, "BuyVehicleButton");
            var source = canvas.Find("UpgradesPanel/EngineUpgradeCard/Content/PurchaseButtonRow").gameObject;
            var buy = Object.Instantiate(source, canvas, false);
            buy.name = "BuyVehicleButton";
            ((RectTransform)buy.transform).Assign(TopCenter(196f, 300f, 64f));
            var label = buy.GetComponentInChildren<Text>(true);
            label.text = "BUY CAR";
            buy.SetActive(false);

            var refs = new SerializedObject(view);
            refs.FindProperty("careerPanel").objectReferenceValue = panel;
            refs.FindProperty("vehicleStatusText").objectReferenceValue = status;
            refs.FindProperty("buyVehicleButton").objectReferenceValue = buy.GetComponent<Button>();
            refs.FindProperty("buyVehicleButtonLabel").objectReferenceValue = label;
            refs.ApplyModifiedPropertiesWithoutUndo();
            Theme(view);

            var trackView = Find<TrackSelectionUguiView>(scene);
            if (trackView == null) return "garage built; Race Select not found.";
            var details = trackView.transform.Find("TrackDetailsPanel");
            Remove(details, "TrackRequirementStatus");
            var requirement = Text(details, "TrackRequirementStatus", string.Empty, TextAnchor.MiddleLeft, TopLeft(38f, 384f, 600f, 40f));
            Role(requirement, ApexUiTextRoleKind.AccentLabel);
            Remove(trackView.transform, "CareerObjectiveStatus");
            var objective = Text(trackView.transform, "CareerObjectiveStatus", string.Empty, TextAnchor.UpperRight, TopRight(64f, 134f, 1100f, 28f));
            Role(objective, ApexUiTextRoleKind.StatLabel);
            var trackRefs = new SerializedObject(trackView);
            trackRefs.FindProperty("requirementText").objectReferenceValue = requirement;
            trackRefs.FindProperty("objectiveText").objectReferenceValue = objective;
            trackRefs.ApplyModifiedPropertiesWithoutUndo();
            Theme(trackView);
            return "career panel, car status, Buy button and Race Select lines built.";
        }

        private static string BuildResults(Scene scene)
        {
            var view = Find<ResultsUguiView>(scene);
            if (view == null) return "ResultsUguiView not found.";
            Remove(view.transform, "CareerResults");

            var block = Panel(view.transform, "CareerResults", new Rect(0f, 0f, 0f, 0f));
            block.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)block.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 520f);
            rect.anchoredPosition = new Vector2(-80f, 0f);

            var title = Text(block.transform, "CareerResultTitle", string.Empty, TextAnchor.UpperLeft, TopLeft(36f, 30f, 688f, 48f));
            Role(title, ApexUiTextRoleKind.PanelTitle);
            Image(block.transform, "CareerResultAccent", ApexUiVisualStyle.PrimaryRed).rectTransform.Assign(TopLeft(38f, 88f, 64f, 6f));
            var body = Text(block.transform, "CareerResultBody", string.Empty, TextAnchor.UpperLeft, TopLeft(38f, 112f, 688f, 380f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            Role(body, ApexUiTextRoleKind.StatValue);

            var refs = new SerializedObject(view);
            refs.FindProperty("careerTitleText").objectReferenceValue = title;
            refs.FindProperty("careerBodyText").objectReferenceValue = body;
            refs.ApplyModifiedPropertiesWithoutUndo();
            Theme(view);
            return "career results block built.";
        }

        /// <summary>Completion title and bar, next objective with its hint, collection counts, final status.</summary>
        private static CareerPanelUguiView BuildCareerPanel(Transform parent, string name, Rect layout, bool withAnnouncement)
        {
            Remove(parent, name);
            var panel = Panel(parent, name, layout);
            var width = layout.width < 0f ? -layout.width : layout.width;
            var inner = width - 64f;

            var completion = Text(panel.transform, "CareerCompletionTitle", "CAREER 0%", TextAnchor.UpperLeft, TopLeft(32f, 22f, inner, 40f));
            Role(completion, ApexUiTextRoleKind.PanelTitle);
            Image(panel.transform, "CareerProgressTrack", ApexUiVisualStyle.Disabled).rectTransform.Assign(TopLeft(32f, 70f, inner, 8f));
            var fill = Image(panel.transform, "CareerProgressFill", ApexUiVisualStyle.PrimaryRed);
            fill.rectTransform.Assign(TopLeft(32f, 70f, inner, 8f));
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            var objectiveTitle = Text(panel.transform, "CareerObjectiveTitle", string.Empty, TextAnchor.UpperLeft, TopLeft(32f, 92f, inner, 28f));
            Role(objectiveTitle, ApexUiTextRoleKind.AccentLabel);
            var objectiveDetail = Text(panel.transform, "CareerObjectiveDetail", string.Empty, TextAnchor.UpperLeft, TopLeft(32f, 122f, inner, 48f));
            objectiveDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectiveDetail.verticalOverflow = VerticalWrapMode.Truncate;
            Role(objectiveDetail, ApexUiTextRoleKind.Hint);
            var collection = Text(panel.transform, "CareerCollectionStatus", string.Empty, TextAnchor.UpperLeft, TopLeft(32f, 174f, inner, 24f));
            Role(collection, ApexUiTextRoleKind.StatLabel);
            var finale = Text(panel.transform, "CareerFinaleStatus", string.Empty, TextAnchor.UpperLeft, TopLeft(32f, 200f, inner, 24f));
            Role(finale, ApexUiTextRoleKind.StatValue);

            Text announcement = null;
            if (withAnnouncement)
            {
                announcement = Text(panel.transform, "CareerAnnouncement", string.Empty, TextAnchor.UpperLeft, TopLeft(32f, 236f, inner, 56f));
                announcement.horizontalOverflow = HorizontalWrapMode.Wrap;
                Role(announcement, ApexUiTextRoleKind.AccentLabel);
            }

            var view = panel.gameObject.AddComponent<CareerPanelUguiView>();
            var refs = new SerializedObject(view);
            refs.FindProperty("root").objectReferenceValue = panel.gameObject;
            refs.FindProperty("completionText").objectReferenceValue = completion;
            refs.FindProperty("completionFill").objectReferenceValue = fill;
            refs.FindProperty("objectiveTitleText").objectReferenceValue = objectiveTitle;
            refs.FindProperty("objectiveDetailText").objectReferenceValue = objectiveDetail;
            refs.FindProperty("collectionText").objectReferenceValue = collection;
            refs.FindProperty("finaleText").objectReferenceValue = finale;
            refs.FindProperty("announcementText").objectReferenceValue = announcement;
            refs.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        private static void Assign(Object target, string property, Object value)
        {
            var refs = new SerializedObject(target);
            refs.FindProperty(property).objectReferenceValue = value;
            refs.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Theme(Component view)
        {
            var theme = view.GetComponentInParent<ApexUiTheme>(true);
            if (theme != null) theme.ApplyStyles();
        }

        private static void Remove(Transform parent, string name)
        {
            var existing = parent != null ? parent.Find(name) : null;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        private static AngledPanelGraphic Panel(Transform parent, string name, Rect layout)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(AngledPanelGraphic));
            panelObject.layer = parent.gameObject.layer;
            panelObject.transform.SetParent(parent, false);
            ((RectTransform)panelObject.transform).Assign(layout);
            var panel = panelObject.GetComponent<AngledPanelGraphic>();
            panel.color = ApexUiVisualStyle.Panel;
            panel.BorderThickness = ApexUiVisualStyle.PanelBorderThickness;
            panel.BorderColor = ApexUiVisualStyle.PanelOutline;
            panel.raycastTarget = false;
            return panel;
        }

        private static Text Text(Transform parent, string name, string value, TextAnchor alignment, Rect layout)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.rectTransform.Assign(layout);
            return text;
        }

        private static Image Image(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.layer = parent.gameObject.layer;
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Role(Text text, ApexUiTextRoleKind role)
        {
            text.gameObject.AddComponent<ApexUiTextRole>().Role = role;
        }

        /// <summary>Rect from the parent's top-left corner (x right, y down).</summary>
        private static Rect TopLeft(float x, float y, float width, float height)
        {
            return new Rect(x, y, width, height);
        }

        /// <summary>Rect from the parent's top-right corner; x is the inset from the right edge.</summary>
        private static Rect TopRight(float right, float y, float width, float height)
        {
            return new Rect(-right, y, -width, height);
        }

        /// <summary>Rect centred horizontally, y from the parent's top edge.</summary>
        private static Rect TopCenter(float y, float width, float height)
        {
            return new Rect(float.NaN, y, width, height);
        }

        private static void Assign(this RectTransform rect, Rect layout)
        {
            // NaN x centres the rect; a negative width anchors it to the right edge (see TopRight).
            var centred = float.IsNaN(layout.x);
            var fromRight = layout.width < 0f;
            var anchor = centred ? new Vector2(0.5f, 1f) : fromRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(Mathf.Abs(layout.width), layout.height);
            rect.anchoredPosition = new Vector2(centred ? 0f : layout.x, -layout.y);
        }
    }
}
