using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class UguiMenuButtonVisualState : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Graphic background;
        [SerializeField] private Text[] labels;
        [SerializeField, Tooltip("Accents the label only. The red fill is reserved for the focused control.")] private bool primaryAction;

        private bool _pressed;
        private bool _isCurrent;

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
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _pressed = false;
            Apply();
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

            var selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
            var interactable = button.IsInteractable();
            var focused = interactable && selected;
            var backgroundColor = !interactable
                ? ApexUiVisualStyle.Disabled
                : focused
                    ? _pressed ? ApexUiVisualStyle.PrimaryRedPressed : ApexUiVisualStyle.PrimaryRed
                    : _isCurrent
                        ? ApexUiVisualStyle.PanelElevated
                        : ApexUiVisualStyle.Panel;
            var textColor = !interactable
                ? ApexUiVisualStyle.DisabledText
                : focused
                    ? ApexUiVisualStyle.FocusedText
                    : primaryAction || _isCurrent
                        ? ApexUiVisualStyle.AccentText
                        : ApexUiVisualStyle.PrimaryText;

            if (background.color != backgroundColor)
            {
                background.color = backgroundColor;
            }

            if (labels == null)
            {
                return;
            }

            for (var index = 0; index < labels.Length; index++)
            {
                if (labels[index] != null)
                {
                    if (labels[index].color != textColor)
                    {
                        labels[index].color = textColor;
                    }
                }
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
