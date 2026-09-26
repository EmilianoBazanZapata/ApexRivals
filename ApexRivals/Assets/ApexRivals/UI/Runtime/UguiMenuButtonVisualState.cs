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
        [SerializeField] private Color normalBackground = new Color32(0x08, 0x0A, 0x0D, 0xC8);
        [SerializeField] private Color highlightedBackground = new Color32(0xD1, 0x14, 0x10, 0xF0);
        [SerializeField] private Color pressedBackground = new Color32(0x7D, 0x08, 0x06, 0xF8);
        [SerializeField] private Color disabledBackground = new Color32(0x25, 0x27, 0x2D, 0xB8);
        [SerializeField] private Color normalText = new Color32(0xF2, 0xF2, 0xEC, 0xFF);
        [SerializeField] private Color highlightedText = Color.white;
        [SerializeField] private Color disabledText = new Color32(0x8B, 0x90, 0x98, 0xFF);

        private bool _pointerInside;
        private bool _pressed;

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
            _pointerInside = false;
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
            _pointerInside = true;
            if (button != null && button.IsInteractable() && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }

            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
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
            var useHighlighted = interactable && selected;
            var backgroundColor = !interactable ? disabledBackground : _pressed && (selected || _pointerInside) ? pressedBackground : useHighlighted ? highlightedBackground : normalBackground;
            var textColor = !interactable ? disabledText : useHighlighted ? highlightedText : normalText;

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
    }
}
