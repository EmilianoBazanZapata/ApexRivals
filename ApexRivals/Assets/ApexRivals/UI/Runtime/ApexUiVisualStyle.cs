using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Small, shared visual token set for the uGUI presentation layer.
    /// Keep this intentionally limited to values that are common across screens.
    /// </summary>
    public static class ApexUiVisualStyle
    {
        public static readonly Color Background = new Color32(0x08, 0x0A, 0x0D, 0xFF);
        public static readonly Color Panel = new Color32(0x17, 0x1B, 0x20, 0xF2);
        public static readonly Color PanelElevated = new Color32(0x1E, 0x24, 0x2B, 0xF7);
        public static readonly Color PrimaryRed = new Color32(0xE5, 0x16, 0x12, 0xFF);
        public static readonly Color PrimaryRedPressed = new Color32(0x9A, 0x0D, 0x0B, 0xFF);
        public static readonly Color PrimaryText = new Color32(0xF2, 0xF2, 0xEC, 0xFF);
        public static readonly Color SecondaryText = new Color32(0x9A, 0xA0, 0xA8, 0xFF);
        public static readonly Color FocusedText = Color.white;
        public static readonly Color AccentText = new Color32(0xFF, 0x3B, 0x35, 0xFF);
        public static readonly Color Disabled = new Color32(0x47, 0x4C, 0x54, 0xC0);
        public static readonly Color DisabledText = new Color32(0x8B, 0x90, 0x98, 0xFF);
        public static readonly Color FocusOutline = new Color32(0xFF, 0x3B, 0x35, 0xFF);
        public static readonly Color PanelOutline = new Color32(0x3C, 0x46, 0x50, 0xFF);
        public static readonly Color Dimmer = new Color32(0x00, 0x00, 0x00, 0xB8);

        public const int ScreenTitleSize = 42;
        public const int SectionTitleSize = 26;
        public const int ButtonLabelSize = 20;
        public const int FieldLabelSize = 16;
        public const int ValueSize = 22;
        public const int SecondaryTextSize = 15;
    }
}
