using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SettingsOptionSelector : Selectable, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Text previousArrowText;
        [SerializeField] private Text valueText;
        [SerializeField] private Text nextArrowText;
        [SerializeField] private RectTransform previousHitTarget;
        [SerializeField] private RectTransform nextHitTarget;
        [SerializeField] private Graphic background;
        [SerializeField] private Color normalBackground = new Color32(0x12, 0x14, 0x18, 0xE8);
        [SerializeField] private Color selectedBackground = new Color32(0xD1, 0x14, 0x10, 0xF0);
        [SerializeField] private Color disabledBackground = new Color32(0x25, 0x27, 0x2D, 0xB8);
        [SerializeField] private Color labelColor = new Color32(0x9A, 0x9F, 0xA8, 0xFF);
        [SerializeField] private Color valueColor = Color.white;
        [SerializeField] private Color selectedValueColor = Color.white;
        [SerializeField] private Color disabledTextColor = new Color32(0x74, 0x78, 0x80, 0xFF);

        private readonly List<string> _options = new List<string>();
        private bool _suppressValueChanged;
        private int _selectedIndex;

        public event System.Action<int> ValueChanged;

        public int SelectedIndex => _selectedIndex;
        public int OptionCount => _options.Count;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            RefreshVisuals();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshVisuals();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RefreshVisuals();
        }

        public void SetLabel(string label)
        {
            if (labelText != null)
            {
                labelText.text = label;
            }

            RefreshVisuals();
        }

        public void SetOptions(IReadOnlyList<string> options, int selectedIndex)
        {
            _options.Clear();
            if (options != null)
            {
                for (var index = 0; index < options.Count; index++)
                {
                    _options.Add(options[index] ?? string.Empty);
                }
            }

            SetSelectedIndexWithoutNotify(selectedIndex);
        }

        public void SetSelectedIndexWithoutNotify(int selectedIndex)
        {
            _suppressValueChanged = true;
            SetSelectedIndex(selectedIndex);
            _suppressValueChanged = false;
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (eventData.moveDir == MoveDirection.Left)
            {
                SelectPrevious();
                eventData.Use();
                return;
            }

            if (eventData.moveDir == MoveDirection.Right)
            {
                SelectNext();
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            SelectSelf();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            RefreshVisuals();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            RefreshVisuals();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            RefreshVisuals();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || !IsInteractable())
            {
                return;
            }

            SelectSelf();

            if (RectContains(previousHitTarget, eventData))
            {
                SelectPrevious();
                return;
            }

            if (RectContains(nextHitTarget, eventData))
            {
                SelectNext();
                return;
            }

            RefreshVisuals();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            RefreshVisuals();
        }

        private void SelectPrevious()
        {
            if (_options.Count <= 1)
            {
                return;
            }

            SetSelectedIndex((_selectedIndex - 1 + _options.Count) % _options.Count);
        }

        private void SelectNext()
        {
            if (_options.Count <= 1)
            {
                return;
            }

            SetSelectedIndex((_selectedIndex + 1) % _options.Count);
        }

        private void SetSelectedIndex(int selectedIndex)
        {
            var clamped = _options.Count == 0 ? 0 : Mathf.Clamp(selectedIndex, 0, _options.Count - 1);
            if (_selectedIndex == clamped && valueText != null && valueText.text == CurrentValueLabel())
            {
                RefreshVisuals();
                return;
            }

            _selectedIndex = clamped;
            RefreshVisuals();

            if (!_suppressValueChanged)
            {
                ValueChanged?.Invoke(_selectedIndex);
            }
        }

        private void SelectSelf()
        {
            if (EventSystem.current != null && !EventSystem.current.alreadySelecting && IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }

            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            ResolveReferences();

            var hasAnyOptions = _options.Count > 0;
            interactable = hasAnyOptions;
            var showInlineArrows = previousArrowText == null && nextArrowText == null;
            if (valueText != null)
            {
                var label = CurrentValueLabel();
                valueText.text = showInlineArrows && label.Length > 0 ? $"< {label} >" : label;
            }

            var selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            var textColor = !hasAnyOptions ? disabledTextColor : selected ? selectedValueColor : valueColor;

            SetGraphicColor(background, !hasAnyOptions ? disabledBackground : selected ? selectedBackground : normalBackground);
            SetTextColor(labelText, labelColor);
            SetTextColor(valueText, textColor);
            SetTextColor(previousArrowText, textColor);
            SetTextColor(nextArrowText, textColor);

            if (previousArrowText != null)
            {
                previousArrowText.text = "<";
            }

            if (nextArrowText != null)
            {
                nextArrowText.text = ">";
            }
        }

        private string CurrentValueLabel()
        {
            return _options.Count == 0 ? string.Empty : _options[Mathf.Clamp(_selectedIndex, 0, _options.Count - 1)];
        }

        private void ResolveReferences()
        {
            if (background == null)
            {
                background = targetGraphic;
            }
        }

        private static bool RectContains(RectTransform rectTransform, PointerEventData eventData)
        {
            return rectTransform != null
                && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position, eventData.pressEventCamera);
        }

        private static void SetGraphicColor(Graphic graphic, Color color)
        {
            if (graphic != null)
            {
                graphic.color = color;
            }
        }

        private static void SetTextColor(Text text, Color color)
        {
            if (text != null)
            {
                text.color = color;
            }
        }
    }
}
