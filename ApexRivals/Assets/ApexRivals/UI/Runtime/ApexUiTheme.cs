using System;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Applies the shared Apex Rivals visual language to one screen canvas.
    /// The optional font slot is deliberately the only typography integration point:
    /// leave it empty to retain the current fallback until an approved font is added.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ApexUiTheme : MonoBehaviour
    {
        [SerializeField] private Font approvedFont;
        [SerializeField] private bool applyOnEnable = true;

        private void OnEnable()
        {
            if (applyOnEnable)
            {
                ApplyStyles();
            }
        }

        private void OnValidate()
        {
            ApplyStyles();
        }

        [ContextMenu("Apply Apex UI Styles")]
        public void ApplyStyles()
        {
            var texts = GetComponentsInChildren<Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                ApplyText(texts[index]);
            }

            var images = GetComponentsInChildren<Image>(true);
            for (var index = 0; index < images.Length; index++)
            {
                ApplyImage(images[index]);
            }

            var outlines = GetComponentsInChildren<Outline>(true);
            for (var index = 0; index < outlines.Length; index++)
            {
                if (outlines[index] != null)
                {
                    outlines[index].effectColor = ApexUiVisualStyle.PanelOutline;
                    outlines[index].effectDistance = new Vector2(1f, -1f);
                }
            }
        }

        private void ApplyText(Text text)
        {
            if (text == null)
            {
                return;
            }

            if (approvedFont != null)
            {
                text.font = approvedFont;
            }

            text.resizeTextForBestFit = false;
            var role = text.GetComponent<ApexUiTextRole>();
            if (role != null)
            {
                ApplyRole(text, role.Role);
                return;
            }

            var name = text.name;
            if (Contains(name, "APEX"))
            {
                SetText(text, ApexUiVisualStyle.PrimaryText, 72, FontStyle.Bold);
            }
            else if (Contains(name, "RIVALS"))
            {
                SetText(text, ApexUiVisualStyle.PrimaryRed, 28, FontStyle.Bold);
            }
            else if (text.GetComponentInParent<Button>(true) != null)
            {
                SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.ButtonLabelSize, FontStyle.Bold);
            }
            else if (Contains(name, "Title") || Contains(name, "Header") || Contains(name, "Pause"))
            {
                SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.ScreenTitleSize, FontStyle.Bold);
            }
            else if (Contains(name, "Value") || Contains(name, "Price") || Contains(name, "Level") || Contains(name, "Benefit") || Contains(name, "Position") || Contains(name, "Lap") || Contains(name, "Time") || Contains(name, "Speed") || Contains(name, "Reward"))
            {
                SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.ValueSize, FontStyle.Bold);
            }
            else if (Contains(name, "Label") || Contains(name, "Description") || Contains(name, "Hint") || Contains(name, "Status") || Contains(name, "Feedback"))
            {
                SetText(text, ApexUiVisualStyle.SecondaryText, ApexUiVisualStyle.SecondaryTextSize, FontStyle.Normal);
            }
            else
            {
                SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.FieldLabelSize, FontStyle.Normal);
            }
        }

        private static void ApplyRole(Text text, ApexUiTextRoleKind role)
        {
            switch (role)
            {
                case ApexUiTextRoleKind.ButtonTitle:
                    SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.MenuButtonTitleSize, FontStyle.Bold);
                    break;
                case ApexUiTextRoleKind.ButtonSubtitle:
                    SetText(text, ApexUiVisualStyle.SecondaryText, ApexUiVisualStyle.MenuButtonSubtitleSize, FontStyle.Normal);
                    break;
                case ApexUiTextRoleKind.PanelTitle:
                    SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.SectionTitleSize, FontStyle.Bold);
                    break;
                case ApexUiTextRoleKind.HudLabel:
                    SetText(text, ApexUiVisualStyle.SecondaryText, ApexUiVisualStyle.SecondaryTextSize, FontStyle.Bold);
                    break;
                case ApexUiTextRoleKind.HudValue:
                    SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.HudValueSize, FontStyle.Bold);
                    break;
                case ApexUiTextRoleKind.Hint:
                    SetText(text, ApexUiVisualStyle.SecondaryText, ApexUiVisualStyle.SecondaryTextSize, FontStyle.Normal);
                    break;
                case ApexUiTextRoleKind.Countdown:
                    SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.CountdownSize, FontStyle.BoldAndItalic);
                    break;
                case ApexUiTextRoleKind.AccentValue:
                    SetText(text, ApexUiVisualStyle.AccentText, ApexUiVisualStyle.HudValueSize, FontStyle.Bold);
                    break;
                case ApexUiTextRoleKind.AccentLabel:
                    SetText(text, ApexUiVisualStyle.AccentText, ApexUiVisualStyle.FieldLabelSize + 2, FontStyle.Bold);
                    break;
                case ApexUiTextRoleKind.StatLabel:
                    SetText(text, ApexUiVisualStyle.SecondaryText, ApexUiVisualStyle.StatTextSize, FontStyle.Normal);
                    break;
                case ApexUiTextRoleKind.StatValue:
                    SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.StatTextSize, FontStyle.Normal);
                    break;
                case ApexUiTextRoleKind.LogoPrimary:
                    SetText(text, ApexUiVisualStyle.PrimaryText, ApexUiVisualStyle.LogoPrimarySize, FontStyle.BoldAndItalic);
                    break;
                case ApexUiTextRoleKind.LogoAccent:
                    SetText(text, ApexUiVisualStyle.PrimaryRed, ApexUiVisualStyle.LogoAccentSize, FontStyle.BoldAndItalic);
                    break;
            }
        }

        private static void ApplyImage(Image image)
        {
            if (image == null || image.GetComponent<Button>() != null)
            {
                return;
            }

            var name = image.name;
            if (Contains(name, "Dimmer"))
            {
                image.color = ApexUiVisualStyle.Dimmer;
            }
            else if (Contains(name, "Accent") || Contains(name, "Stripe"))
            {
                image.color = ApexUiVisualStyle.PrimaryRed;
            }
            else if (Contains(name, "Segment"))
            {
                return;
            }
            else if (Contains(name, "Panel") || Contains(name, "Card") || Contains(name, "Background") || Contains(name, "Dialog"))
            {
                image.color = ApexUiVisualStyle.Panel;
            }
        }

        private static void SetText(Text text, Color color, int size, FontStyle style)
        {
            text.color = color;
            text.fontSize = size;
            text.fontStyle = style;
        }

        private static bool Contains(string value, string fragment)
        {
            return value != null && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
