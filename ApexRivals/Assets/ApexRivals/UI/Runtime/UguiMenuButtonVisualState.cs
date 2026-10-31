using ApexRivals.Audio.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class UguiMenuButtonVisualState : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private Button button;
        [SerializeField] private Graphic background;
        [SerializeField] private Text[] labels;
        [SerializeField, Tooltip("Non-text graphics (icons) tinted like the primary label.")] private Graphic[] icons;
        [SerializeField, Tooltip("Accents the label only. The red fill is reserved for the focused control.")] private bool primaryAction;
        [SerializeField, Tooltip("Hero call-to-action (e.g. START RACE): rests in a dim red fill and brightens to the full red on focus.")] private bool filledPrimary;
        [SerializeField, Tooltip("The current entry (e.g. the selected track card) rests in the dim red fill instead of the elevated panel.")] private bool currentUsesAccentFill;

        private bool _pressed;
        private bool _isCurrent;
        private bool _deselecting;
        private bool[] _secondaryLabels;

        public bool PrimaryAction
        {
            get => primaryAction;
            set
            {
                primaryAction = value;
                Apply();
            }
        }

        /// <summary>
        /// Marks the active tab/section. Shown with an elevated panel and accent label, never with the focus fill.
        /// </summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                _isCurrent = value;
                Apply();
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Reset()
        {
            button = GetComponent<Button>();
            background = GetComponent<Graphic>();
            labels = GetComponentsInChildren<Text>(true);
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            _pressed = false;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _pressed = false;
            Apply();
            UiAudioEvents.Raise(UiSound.Move);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && button != null && button.IsInteractable())
            {
                UiAudioEvents.Raise(UiSound.Confirm);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                UiAudioEvents.Raise(UiSound.Confirm);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            // The EventSystem still reports this button as selected while it raises OnDeselect.
            _pressed = false;
            _deselecting = true;
            Apply();
            _deselecting = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hover only moves focus while the mouse drives the UI; a hidden, resting cursor must not steal it.
            // UiFocus also refuses targets behind an open modal.
            if (UiInputModality.IsMouse)
            {
                UiFocus.Request(button);
            }

            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            Apply();
        }

        private void Apply()
        {
            if (button == null || background == null)
            {
                return;
            }

            var selected = !_deselecting && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
            var interactable = button.IsInteractable();
            var focused = interactable && selected;
            // Dim-red resting fills stay clearly darker than the bright focus fill, so focus is never ambiguous.
            var restingAccentFill = filledPrimary || (_isCurrent && currentUsesAccentFill);
            var backgroundColor = !interactable
                ? Palette.Disabled
                : focused
                    ? _pressed ? Palette.PrimaryRedPressed : Palette.PrimaryRed
                    : restingAccentFill
                        ? Palette.PrimaryRedIdle
                        : _isCurrent
                            ? Palette.PanelElevated
                            : Palette.Panel;
            var textColor = !interactable
                ? Palette.DisabledText
                : focused
                    ? Palette.FocusedText
                    : restingAccentFill
                        ? Palette.PrimaryText
                        : primaryAction || _isCurrent
                            ? Palette.AccentText
                            : Palette.PrimaryText;

            if (background.color != backgroundColor)
            {
                background.color = backgroundColor;
            }

            if (background is AngledPanelGraphic angledBackground && angledBackground.BorderThickness > 0f)
            {
                // The focus fill already separates the control; the border only outlines resting panels.
                angledBackground.BorderColor = interactable && !focused ? Palette.PanelOutline : Color.clear;
            }

            var restingSecondary = interactable && !focused;
            if (labels != null)
            {
                EnsureSecondaryLabelCache();
                for (var index = 0; index < labels.Length; index++)
                {
                    var color = restingSecondary && _secondaryLabels[index] ? Palette.SecondaryText : textColor;
                    SetColor(labels[index], color);
                }
            }

            if (icons != null)
            {
                for (var index = 0; index < icons.Length; index++)
                {
                    SetColor(icons[index], textColor);
                }
            }
        }

        private void EnsureSecondaryLabelCache()
        {
            if (_secondaryLabels != null && _secondaryLabels.Length == labels.Length)
            {
                return;
            }

            _secondaryLabels = new bool[labels.Length];
            for (var index = 0; index < labels.Length; index++)
            {
                var role = labels[index] != null ? labels[index].GetComponent<ApexUiTextRole>() : null;
                _secondaryLabels[index] = role != null && role.UsesSecondaryRestingColor;
            }
        }

        private static void SetColor(Graphic graphic, Color color)
        {
            if (graphic != null && graphic.color != color)
            {
                graphic.color = color;
            }
        }

        private void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (background == null && button != null)
            {
                background = button.targetGraphic;
            }

        }
    }
}
