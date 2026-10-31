using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Settings row frame: outlines the row in red and brightens its label while the row's control (option
    /// selector or slider) has focus. Hovering anywhere on the row focuses that control in mouse modality.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsRowHighlight : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private Selectable target;
        [SerializeField] private AngledPanelGraphic frame;
        [SerializeField] private Graphic accent;
        [SerializeField] private Text label;

        private bool? _focused;
        private bool _interactable;

        private void OnEnable()
        {
            _focused = null;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiInputModality.IsMouse && target != null)
            {
                UiFocus.Request(target);
            }
        }

        private void Apply()
        {
            var interactable = target != null && target.IsInteractable();
            var focused = interactable && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == target.gameObject;
            if (_focused == focused && _interactable == interactable)
            {
                return;
            }

            _focused = focused;
            _interactable = interactable;
            if (frame != null)
            {
                frame.color = focused ? Palette.PanelElevated : Palette.Panel;
                frame.BorderColor = focused ? Palette.PrimaryRed : Palette.PanelOutline;
            }

            if (accent != null)
            {
                accent.color = focused ? Palette.PrimaryRed : Palette.PanelOutline;
            }

            if (label != null)
            {
                label.color = !interactable ? Palette.DisabledText : focused ? Palette.PrimaryText : Palette.SecondaryText;
            }
        }
    }
}
