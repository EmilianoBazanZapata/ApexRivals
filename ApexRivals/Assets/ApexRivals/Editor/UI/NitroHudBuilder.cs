using System;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Editor.UI
{
    /// <summary>Explicit authoring helper for the race HUD, invoked through Unity Editor tools.</summary>
    public static class NitroHudBuilder
    {
        public static NitroHudUguiView Create(RaceHudUguiView hud)
        {
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            var existing = new SerializedObject(hud).FindProperty("nitroView").objectReferenceValue as NitroHudUguiView;
            if (existing != null) return existing;
            if (hud.transform.Find("NitroPanel") != null) throw new InvalidOperationException("NitroPanel already exists in " + hud.gameObject.scene.path);
            var sample = hud.GetComponentInChildren<Text>(true);
            if (sample == null || sample.font == null) throw new InvalidOperationException("Race HUD has no reference font.");
            var panel = Rect(hud.transform, "NitroPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(360f, 100f), new Vector2(0f, 38f));
            var background = panel.gameObject.AddComponent<AngledPanelGraphic>();
            background.color = ApexUiVisualStyle.Panel;
            background.BorderColor = ApexUiVisualStyle.PanelOutline;
            background.BorderThickness = ApexUiVisualStyle.PanelBorderThickness;
            background.RightTopInset = 12f;
            background.raycastTarget = false;
            var title = Label(panel, sample.font, "NitroStatus", "NITRO", new Vector2(16f, 64f), new Vector2(240f, 28f), TextAnchor.MiddleLeft, 20);
            var value = Label(panel, sample.font, "NitroCharge", "0%", new Vector2(278f, 64f), new Vector2(66f, 28f), TextAnchor.MiddleRight, 20);
            Label(panel, sample.font, "NitroHint", "DRIFT TO CHARGE  |  HOLD SHIFT / X", new Vector2(16f, 8f), new Vector2(328f, 22f), TextAnchor.MiddleCenter, 13);
            var track = Rect(panel, "NitroTrack", Vector2.zero, Vector2.zero, new Vector2(328f, 18f), new Vector2(16f, 37f));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = ApexUiVisualStyle.Disabled;
            trackImage.raycastTarget = false;
            var fill = Rect(track, "NitroFill", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMax = new Vector2(0f, 1f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = ApexUiVisualStyle.PrimaryRed;
            fillImage.raycastTarget = false;
            var view = panel.gameObject.AddComponent<NitroHudUguiView>();
            var refs = new SerializedObject(view);
            refs.FindProperty("chargeFill").objectReferenceValue = fillImage;
            refs.FindProperty("statusText").objectReferenceValue = title;
            refs.FindProperty("chargeText").objectReferenceValue = value;
            refs.ApplyModifiedProperties();
            var hudRefs = new SerializedObject(hud);
            hudRefs.FindProperty("nitroView").objectReferenceValue = view;
            hudRefs.ApplyModifiedProperties();
            return view;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Text Label(Transform parent, Font font, string name, string text, Vector2 position, Vector2 size, TextAnchor alignment, int fontSize)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.zero, size, position);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = ApexUiVisualStyle.PrimaryText;
            label.alignment = alignment;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }
    }
}
