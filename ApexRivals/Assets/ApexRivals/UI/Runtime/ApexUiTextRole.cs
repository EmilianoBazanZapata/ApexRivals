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
        StatValue,

        /// <summary>Styled by the screen builder itself; the editor styling pass leaves it alone.</summary>
        Authored
    }

    /// <summary>
    /// Declares the typographic role of a uGUI text: the screen builders' editor styling pass styles it by role
    /// instead of inferring the style from the GameObject name, and button subtitles rest in the secondary color.
    /// Only data; it never restyles the text itself.
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
