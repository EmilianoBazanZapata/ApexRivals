using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// The colors and border widths UI controls switch between as their state changes (focused, current, locked,
    /// the player's row...). One asset, edited in the Inspector, referenced by every such control, so the look is
    /// tuned in the editor instead of in code. Defaults match <see cref="ApexUiVisualStyle"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ApexRivals/UI/Palette", fileName = "ApexUiPalette")]
    public sealed class ApexUiPalette : ScriptableObject
    {
        [Header("Surfaces")]
        [SerializeField] private Color panel = ApexUiVisualStyle.Panel;
        [SerializeField] private Color panelElevated = ApexUiVisualStyle.PanelElevated;
        [SerializeField] private Color panelDeep = ApexUiVisualStyle.PanelDeep;
        [SerializeField] private Color panelOutline = ApexUiVisualStyle.PanelOutline;
        [SerializeField] private Color disabled = ApexUiVisualStyle.Disabled;

        [Header("Accent")]
        [SerializeField] private Color primaryRed = ApexUiVisualStyle.PrimaryRed;
        [SerializeField] private Color primaryRedPressed = ApexUiVisualStyle.PrimaryRedPressed;
        [SerializeField, Tooltip("Dim red resting fill (hero buttons, the player's results row).")] private Color primaryRedIdle = ApexUiVisualStyle.PrimaryRedIdle;
        [SerializeField] private Color focusOutline = ApexUiVisualStyle.FocusOutline;
        [SerializeField] private Color successGreen = ApexUiVisualStyle.SuccessGreen;

        [Header("Text")]
        [SerializeField] private Color primaryText = ApexUiVisualStyle.PrimaryText;
        [SerializeField] private Color secondaryText = ApexUiVisualStyle.SecondaryText;
        [SerializeField] private Color focusedText = ApexUiVisualStyle.FocusedText;
        [SerializeField] private Color accentText = ApexUiVisualStyle.AccentText;
        [SerializeField] private Color disabledText = ApexUiVisualStyle.DisabledText;

        [Header("Thumbnails")]
        [SerializeField, Tooltip("State tint multiplied by each Image's authored color.")] private Color availableThumbnailTint = Color.white;
        [SerializeField, Tooltip("Tint of a locked track's preview.")] private Color lockedThumbnailTint = new Color(0.35f, 0.35f, 0.38f, 1f);
        [SerializeField, Tooltip("Tint of a car that cannot be selected yet.")] private Color unavailableThumbnailTint = new Color(0.45f, 0.45f, 0.48f, 1f);

        [Header("Borders")]
        [SerializeField, Min(0f)] private float cardBorderResting = 2f;
        [SerializeField, Min(0f)] private float cardBorderSelected = 4f;
        [SerializeField, Min(0f)] private float vehicleFrameBorderResting = 1.5f;
        [SerializeField, Min(0f)] private float vehicleFrameBorderSelected = 3f;

        private static ApexUiPalette _fallback;

        public Color Panel => panel;
        public Color PanelElevated => panelElevated;
        public Color PanelDeep => panelDeep;
        public Color PanelOutline => panelOutline;
        public Color Disabled => disabled;
        public Color PrimaryRed => primaryRed;
        public Color PrimaryRedPressed => primaryRedPressed;
        public Color PrimaryRedIdle => primaryRedIdle;
        public Color FocusOutline => focusOutline;
        public Color SuccessGreen => successGreen;
        public Color PrimaryText => primaryText;
        public Color SecondaryText => secondaryText;
        public Color FocusedText => focusedText;
        public Color AccentText => accentText;
        public Color DisabledText => disabledText;
        public Color AvailableThumbnailTint => availableThumbnailTint;
        public Color LockedThumbnailTint => lockedThumbnailTint;
        public Color UnavailableThumbnailTint => unavailableThumbnailTint;
        public float CardBorderResting => cardBorderResting;
        public float CardBorderSelected => cardBorderSelected;
        public float VehicleFrameBorderResting => vehicleFrameBorderResting;
        public float VehicleFrameBorderSelected => vehicleFrameBorderSelected;

        /// <summary>Rich-text hex of <see cref="PrimaryRed"/> for two-tone titles.</summary>
        public string PrimaryRedHex => "#" + ColorUtility.ToHtmlStringRGB(primaryRed);

        /// <summary>
        /// The assigned palette, or the defaults when a control was left without one (it still draws correctly;
        /// assign the project palette to tune it).
        /// </summary>
        public static ApexUiPalette Resolve(ApexUiPalette assigned)
        {
            if (assigned != null) return assigned;
            if (_fallback == null)
            {
                _fallback = CreateInstance<ApexUiPalette>();
                _fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _fallback;
        }
    }
}
