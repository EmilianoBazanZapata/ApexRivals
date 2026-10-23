using ApexRivals.Tracks.Configuration;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Editor.UI
{
    /// <summary>
    /// (Re)builds the Race Select canvas in the Garage scene and wires it to the garage installer and screen
    /// navigator. Footer buttons and carousel arrows are cloned from the garage so both screens share one
    /// look; everything else uses the shared theme (<see cref="ApexUiTheme"/>, <see cref="AngledPanelGraphic"/>).
    /// Layout is authored in 1920x1080 reference pixels from the top-left corner.
    /// </summary>
    public static class TrackSelectScreenBuilder
    {
        private const string GarageScenePath = "Assets/ApexRivals/Scenes/Garage.unity";
        private const string CatalogPath = "Assets/ApexRivals/Content/Tracks/TrackCatalogDefinition.asset";
        private const string CanvasName = "TrackSelectCanvas";
        private const float CardWidth = 312f;
        private const float CardHeight = 200f;
        private const float CardSpacing = 15f;
        private const float CardStripHeight = 56f;

        [MenuItem("ApexRivals/UI/Rebuild Track Select Screen")]
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
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != GarageScenePath)
            {
                scene = EditorSceneManager.OpenScene(GarageScenePath, OpenSceneMode.Single);
            }

            var garageCanvas = GameObject.Find("GarageCanvas");
            var installer = Object.FindFirstObjectByType<GarageUiSceneInstaller>(FindObjectsInactive.Include);
            var navigator = installer != null ? installer.GetComponent<UguiScreenNavigator>() : null;
            if (garageCanvas == null || installer == null || navigator == null)
            {
                return "Garage scene is missing GarageCanvas or its UI installer.";
            }

            var existing = garageCanvas.transform.parent.Find(CanvasName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var root = BuildCanvas(garageCanvas);
            var view = root.GetComponent<TrackSelectionUguiView>();
            var refs = new SerializedObject(view);

            Stretch(Image(root.transform, "Dimmer", ApexUiVisualStyle.Dimmer).rectTransform);
            BuildHeader(root.transform);
            BuildPreview(root.transform, refs);
            BuildDetails(root.transform, refs);
            BuildCarousel(root.transform, garageCanvas.transform, refs);
            BuildFooter(root.transform, garageCanvas.transform, refs);
            refs.FindProperty("visibleCardCount").intValue = 5;
            refs.ApplyModifiedPropertiesWithoutUndo();

            root.GetComponent<ApexUiTheme>().ApplyStyles();
            PlaceTitleAccent(root.transform);

            var installerRefs = new SerializedObject(installer);
            installerRefs.FindProperty("trackSelectionView").objectReferenceValue = view;
            installerRefs.FindProperty("trackCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TrackCatalogDefinition>(CatalogPath);
            installerRefs.ApplyModifiedPropertiesWithoutUndo();

            var navigatorRefs = new SerializedObject(navigator);
            navigatorRefs.FindProperty("trackSelectionPanel").objectReferenceValue = root;
            navigatorRefs.FindProperty("trackSelectionInitialSelection").objectReferenceValue = refs.FindProperty("startRaceButton").objectReferenceValue;
            navigatorRefs.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Track select screen rebuilt in {scene.path}.";
        }

        private static GameObject BuildCanvas(GameObject garageCanvas)
        {
            var root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = garageCanvas.layer;
            root.transform.SetParent(garageCanvas.transform.parent, false);
            root.transform.SetSiblingIndex(garageCanvas.transform.GetSiblingIndex() + 1);
            var canvas = root.GetComponent<Canvas>();
            var source = garageCanvas.GetComponent<Canvas>();
            canvas.renderMode = source.renderMode;
            canvas.sortingOrder = source.sortingOrder;
            EditorUtility.CopySerialized(garageCanvas.GetComponent<CanvasScaler>(), root.GetComponent<CanvasScaler>());
            root.AddComponent<TrackSelectionUguiView>();
            var theme = root.AddComponent<ApexUiTheme>();
            EditorUtility.CopySerialized(garageCanvas.GetComponent<ApexUiTheme>(), theme);
            return root;
        }

        private static void BuildHeader(Transform root)
        {
            // Same name-based logo styles as the other screens' compact logos (APEX white, RIVALS red).
            Text(root, "LogoApex", "APEX", TextAnchor.UpperLeft, TopLeft(64f, 30f, 420f, 84f));
            Text(root, "LogoRivals", "RIVALS", TextAnchor.UpperLeft, TopLeft(70f, 108f, 420f, 36f));

            Text(root, "ScreenTitle", "RACE SELECT", TextAnchor.UpperRight, TopRight(64f, 36f, 640f, 56f));
            var subtitle = Text(root, "ScreenSubtitle", "CHOOSE A TRACK", TextAnchor.UpperRight, TopRight(64f, 98f, 640f, 28f));
            Role(subtitle, ApexUiTextRoleKind.StatLabel);
            var accent = Panel(root, "TitleAccent", TopRight(0f, 40f, 16f, 48f), ApexUiVisualStyle.PrimaryRed, 0f);
            accent.LeftBottomInset = 0f;
            accent.LeftTopInset = 9f;
            accent.RightBottomInset = 9f;
            accent.raycastTarget = false;
        }

        private static void BuildPreview(Transform root, SerializedObject refs)
        {
            var frame = Panel(root, "PreviewFrame", TopLeft(150f, 175f, 940f, 440f), ApexUiVisualStyle.Panel, ApexUiVisualStyle.PanelBorderThickness);
            var placeholder = Text(frame.transform, "PreviewPlaceholderHint", "PREVIEW UNAVAILABLE", TextAnchor.MiddleCenter, null);
            Stretch(placeholder.rectTransform);
            var image = Image(frame.transform, "PreviewImage", Color.white);
            Stretch(image.rectTransform, 4f);
            image.preserveAspect = false;
            refs.FindProperty("previewImage").objectReferenceValue = image;
            refs.FindProperty("previewPlaceholder").objectReferenceValue = placeholder.gameObject;
        }

        private static void BuildDetails(Transform root, SerializedObject refs)
        {
            var panel = Panel(root, "TrackDetailsPanel", TopLeft(1110f, 175f, 660f, 440f), ApexUiVisualStyle.Panel, ApexUiVisualStyle.PanelBorderThickness).transform;

            var name = Text(panel, "TrackNameTitle", string.Empty, TextAnchor.UpperLeft, TopLeft(36f, 30f, 420f, 56f));
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            var location = Text(panel, "TrackLocation", string.Empty, TextAnchor.UpperLeft, TopLeft(38f, 90f, 380f, 28f));
            Role(location, ApexUiTextRoleKind.StatLabel);
            Image(panel, "TrackAccent", ApexUiVisualStyle.PrimaryRed).rectTransform.Assign(TopLeft(38f, 130f, 64f, 6f));

            string[] labels = { "LENGTH", "LAPS", "ENVIRONMENT", "SURFACE" };
            string[] fields = { "lengthValueText", "lapsValueText", "environmentValueText", "surfaceValueText" };
            for (var row = 0; row < labels.Length; row++)
            {
                var top = 168f + row * 52f;
                var label = Text(panel, labels[row][0] + labels[row].Substring(1).ToLowerInvariant() + "StatLabel", labels[row], TextAnchor.MiddleLeft, TopLeft(38f, top, 180f, 48f));
                Role(label, ApexUiTextRoleKind.StatLabel);
                var value = Text(panel, labels[row][0] + labels[row].Substring(1).ToLowerInvariant() + "Value", "-", TextAnchor.MiddleLeft, TopLeft(222f, top, 200f, 48f));
                refs.FindProperty(fields[row]).objectReferenceValue = value;
                Image(panel, "StatRowDivider", ApexUiVisualStyle.PanelOutline).rectTransform.Assign(TopLeft(38f, top + 49f, 384f, 1f));
            }

            var minimapObject = new GameObject("TrackMinimap", typeof(RectTransform), typeof(CanvasRenderer), typeof(TrackMinimapGraphic));
            minimapObject.layer = panel.gameObject.layer;
            minimapObject.transform.SetParent(panel, false);
            ((RectTransform)minimapObject.transform).Assign(TopLeft(436f, 36f, 200f, 368f));
            var minimap = minimapObject.GetComponent<TrackMinimapGraphic>();
            minimap.color = ApexUiVisualStyle.PrimaryText;
            minimap.MarkerColor = ApexUiVisualStyle.PrimaryText;
            minimap.MarkerAlternateColor = ApexUiVisualStyle.PrimaryRed;
            minimap.raycastTarget = false;

            refs.FindProperty("trackNameText").objectReferenceValue = name;
            refs.FindProperty("locationText").objectReferenceValue = location;
            refs.FindProperty("minimap").objectReferenceValue = minimap;
        }

        private static void BuildCarousel(Transform root, Transform garageCanvas, SerializedObject refs)
        {
            var carousel = new GameObject("TrackCarousel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            carousel.layer = root.gameObject.layer;
            carousel.transform.SetParent(root, false);
            var carouselRect = (RectTransform)carousel.transform;
            carouselRect.Assign(TopLeft(150f, 645f, 1620f, CardHeight));
            var layout = carousel.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = CardSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var card = BuildCardTemplate(carousel.transform);
            refs.FindProperty("cardContainer").objectReferenceValue = carouselRect;
            refs.FindProperty("cardTemplate").objectReferenceValue = card;

            var previous = CloneButton(garageCanvas, "VehicleSelector/PreviousVehicleButton", root, "PreviousTrackButton");
            var next = CloneButton(garageCanvas, "VehicleSelector/NextVehicleButton", root, "NextTrackButton");
            ((RectTransform)previous.transform).Assign(TopLeft(48f, 645f + (CardHeight - 84f) * 0.5f, 84f, 84f));
            ((RectTransform)next.transform).Assign(TopLeft(1788f, 645f + (CardHeight - 84f) * 0.5f, 84f, 84f));
            refs.FindProperty("previousTrackButton").objectReferenceValue = previous;
            refs.FindProperty("nextTrackButton").objectReferenceValue = next;
        }

        private static TrackSelectionCardView BuildCardTemplate(Transform parent)
        {
            var card = new GameObject("TrackCardTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(AngledPanelGraphic), typeof(Button), typeof(TrackSelectionCardView));
            card.layer = parent.gameObject.layer;
            card.transform.SetParent(parent, false);
            var rect = (RectTransform)card.transform;
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            var frame = card.GetComponent<AngledPanelGraphic>();
            frame.color = ApexUiVisualStyle.Panel;
            frame.BorderThickness = 3f;
            frame.BorderColor = ApexUiVisualStyle.PanelOutline;

            var placeholder = Text(card.transform, "ThumbnailPlaceholderHint", "NO PREVIEW", TextAnchor.MiddleCenter, null);
            Stretch(placeholder.rectTransform, 3f);
            placeholder.rectTransform.offsetMin = new Vector2(3f, CardStripHeight);
            var thumbnail = Image(card.transform, "Thumbnail", Color.white);
            Stretch(thumbnail.rectTransform, 3f);
            thumbnail.rectTransform.offsetMin = new Vector2(3f, CardStripHeight);
            thumbnail.raycastTarget = false;

            var stripObject = new GameObject("NameStrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(AngledPanelGraphic));
            stripObject.layer = card.layer;
            stripObject.transform.SetParent(card.transform, false);
            var stripRect = (RectTransform)stripObject.transform;
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.pivot = new Vector2(0.5f, 0f);
            stripRect.offsetMin = new Vector2(3f, 3f);
            stripRect.offsetMax = new Vector2(-3f, CardStripHeight);
            var strip = stripObject.GetComponent<AngledPanelGraphic>();
            strip.color = ApexUiVisualStyle.Panel;
            strip.raycastTarget = false;

            // "Value" picks the theme's bold value style; the visual state owns the colour.
            var label = Text(stripObject.transform, "TrackNameValue", string.Empty, TextAnchor.MiddleLeft, null);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(18f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);

            var button = card.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;

            var visual = card.AddComponent<UguiMenuButtonVisualState>();
            var visualRefs = new SerializedObject(visual);
            visualRefs.FindProperty("button").objectReferenceValue = button;
            visualRefs.FindProperty("background").objectReferenceValue = strip;
            var labels = visualRefs.FindProperty("labels");
            labels.arraySize = 1;
            labels.GetArrayElementAtIndex(0).objectReferenceValue = label;
            visualRefs.ApplyModifiedPropertiesWithoutUndo();

            var view = card.GetComponent<TrackSelectionCardView>();
            var cardRefs = new SerializedObject(view);
            cardRefs.FindProperty("button").objectReferenceValue = button;
            cardRefs.FindProperty("thumbnail").objectReferenceValue = thumbnail;
            cardRefs.FindProperty("thumbnailPlaceholder").objectReferenceValue = placeholder.gameObject;
            cardRefs.FindProperty("nameText").objectReferenceValue = label;
            cardRefs.FindProperty("visualState").objectReferenceValue = visual;
            cardRefs.FindProperty("selectionFrame").objectReferenceValue = frame;
            cardRefs.ApplyModifiedPropertiesWithoutUndo();

            card.SetActive(false);
            return view;
        }

        private static void BuildFooter(Transform root, Transform garageCanvas, SerializedObject refs)
        {
            var footer = Object.Instantiate(garageCanvas.Find("NavigationFooter").gameObject, root, false);
            footer.name = "NavigationFooter";
            var hints = footer.transform.Find("InputHints").GetComponent<Text>();
            hints.text = "ENTER / A: SELECT     ESC / B: BACK     Q / LB  E / RB: CHANGE TRACK";
            refs.FindProperty("backButton").objectReferenceValue = footer.transform.Find("BackButton").GetComponent<Button>();
            refs.FindProperty("startRaceButton").objectReferenceValue = footer.transform.Find("StartRaceButton").GetComponent<Button>();

            var feedback = Object.Instantiate(garageCanvas.Find("PurchaseFeedback").gameObject, root, false);
            feedback.name = "RaceFeedback";
            ((RectTransform)feedback.transform).anchoredPosition = new Vector2(0f, 190f);
            feedback.SetActive(false);
            refs.FindProperty("feedbackRoot").objectReferenceValue = feedback;
            refs.FindProperty("statusText").objectReferenceValue = feedback.GetComponentInChildren<Text>(true);
        }

        private static void PlaceTitleAccent(Transform root)
        {
            var title = root.Find("ScreenTitle").GetComponent<Text>();
            var accent = (RectTransform)root.Find("TitleAccent");
            var rightInset = -((RectTransform)title.transform).anchoredPosition.x;
            accent.anchoredPosition = new Vector2(-(rightInset + title.preferredWidth + 22f), accent.anchoredPosition.y);
        }

        private static Button CloneButton(Transform garageCanvas, string path, Transform parent, string name)
        {
            var clone = Object.Instantiate(garageCanvas.Find(path).gameObject, parent, false);
            clone.name = name;
            return clone.GetComponent<Button>();
        }

        private static Text Text(Transform parent, string name, string value, TextAnchor alignment, Rect? layout)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (layout.HasValue)
            {
                text.rectTransform.Assign(layout.Value);
            }

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

        private static AngledPanelGraphic Panel(Transform parent, string name, Rect layout, Color color, float border)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(AngledPanelGraphic));
            panelObject.layer = parent.gameObject.layer;
            panelObject.transform.SetParent(parent, false);
            ((RectTransform)panelObject.transform).Assign(layout);
            var panel = panelObject.GetComponent<AngledPanelGraphic>();
            panel.color = color;
            panel.BorderThickness = border;
            panel.BorderColor = border > 0f ? ApexUiVisualStyle.PanelOutline : Color.clear;
            panel.raycastTarget = false;
            return panel;
        }

        private static void Role(Text text, ApexUiTextRoleKind role)
        {
            text.gameObject.AddComponent<ApexUiTextRole>().Role = role;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Rect measured from the parent's top-left corner (x right, y down).</summary>
        private static Rect TopLeft(float x, float y, float width, float height)
        {
            return new Rect(x, y, width, height);
        }

        /// <summary>Rect measured from the parent's top-right corner; x is the inset from the right edge.</summary>
        private static Rect TopRight(float right, float y, float width, float height)
        {
            return new Rect(-right, y, -width, height);
        }

        private static void Assign(this RectTransform rect, Rect layout)
        {
            // Negative width marks a right-anchored rect (see TopRight).
            var fromRight = layout.width < 0f;
            var anchor = fromRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(Mathf.Abs(layout.width), layout.height);
            rect.anchoredPosition = new Vector2(layout.x, -layout.y);
        }
    }
}
