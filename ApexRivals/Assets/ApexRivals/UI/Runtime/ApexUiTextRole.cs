using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    public enum ApexUiTextRoleKind
    {
        ButtonTitle,
        ButtonSubtitle,
        LogoPrimary,
        LogoAccent,
        PanelTitle,
        HudLabel,
        HudValue,
        Hint,
        Countdown,
        AccentValue,
        AccentLabel,
        StatLabel,
        StatValue
    }

    /// <summary>
    /// Declares the typographic role of a uGUI text so <see cref="ApexUiTheme"/> styles it explicitly
    /// instead of inferring the style from the GameObject name.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class ApexUiTextRole : MonoBehaviour
    {
        [SerializeField] private ApexUiTextRoleKind role;

        public ApexUiTextRoleKind Role
        {
            get => role;
            set => role = value;
        }

        /// <summary>
        /// Subtitles rest in the secondary color and only brighten while their button has focus.
        /// </summary>
        public bool UsesSecondaryRestingColor => role == ApexUiTextRoleKind.ButtonSubtitle;
    }
}
