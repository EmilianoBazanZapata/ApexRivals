using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Option row for discrete settings. Up and down move between rows; left and
    /// right change the value. Hover selects the row and arrow clicks change one step.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsOptionSelector : Selectable, IPointerClickHandler
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Text previousArrowText;
        [SerializeField] private Text valueText;
        [SerializeField] private Text nextArrowText;
        [SerializeField] private RectTransform previousHitTarget;
        [SerializeField] private RectTransform nextHitTarget;
        [SerializeField] private Graphic background;
        [SerializeField] private bool wrapAround = true;

        private readonly List<string> _options = new List<string>();
        private int _selectedIndex;
        private bool _isSelected;

        public event System.Action<int> ValueChanged;

        public int SelectedIndex => _selectedIndex;
        public int OptionCount => _options.Count;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
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
            _isSelected = false;
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
            _selectedIndex = ClampIndex(selectedIndex);
            RefreshVisuals();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                if (IsInteractable())
                {
                    Step(eventData.moveDir == MoveDirection.Left ? -1 : 1);
                }

                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (UiInputModality.IsMouse)
            {
                SelectSelf();
            }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            RefreshVisuals();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            base.OnSelect(eventData);
            RefreshVisuals();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            base.OnDeselect(eventData);
            RefreshVisuals();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null
                || eventData.button != PointerEventData.InputButton.Left
                || !IsInteractable())
            {
                return;
            }

            SelectSelf();

            if (RectContains(previousHitTarget, eventData))
            {
                Step(-1);
                return;
            }

            if (RectContains(nextHitTarget, eventData))
            {
                Step(1);
                return;
            }

            if (previousHitTarget == null && nextHitTarget == null && RectContainsInlineArrow(eventData, out var direction))
            {
                Step(direction);
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            RefreshVisuals();
        }

        private void Step(int direction)
        {
            var count = _options.Count;
            if (!IsInteractable() || count <= 1)
            {
                return;
            }

            var next = _selectedIndex + direction;
            if (wrapAround)
            {
                next = (next % count + count) % count;
            }
            else
            {
                next = Mathf.Clamp(next, 0, count - 1);
            }

            if (next == _selectedIndex)
            {
                return;
            }

            _selectedIndex = next;
            RefreshVisuals();
            ValueChanged?.Invoke(_selectedIndex);
        }

        private int ClampIndex(int index)
        {
            return _options.Count == 0 ? 0 : Mathf.Clamp(index, 0, _options.Count - 1);
        }

        private void SelectSelf()
        {
            UiFocus.Request(this);

            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            ResolveReferences();

            var enabledRow = IsInteractable() && _options.Count > 0;
            var highlighted = enabledRow && _isSelected;
            var inlineArrows = previousArrowText == null && nextArrowText == null;
            if (valueText != null)
            {
                var label = CurrentValueLabel();
                valueText.text = inlineArrows && label.Length > 0 ? $"< {label} >" : label;
            }

            var textColor = !enabledRow ? ApexUiVisualStyle.DisabledText : ApexUiVisualStyle.PrimaryText;
            SetGraphicColor(background, !enabledRow ? ApexUiVisualStyle.Disabled : highlighted ? ApexUiVisualStyle.PrimaryRed : ApexUiVisualStyle.PanelElevated);
            SetTextColor(labelText, ApexUiVisualStyle.SecondaryText);
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
            return _options.Count == 0 ? string.Empty : _options[ClampIndex(_selectedIndex)];
        }

        private void ResolveReferences()
        {
            if (background == null)
            {
                background = targetGraphic;
            }

            if (valueText == null)
            {
                valueText = FindChildText("Value", "Label");
            }

            if (previousArrowText == null)
            {
                previousArrowText = FindChildText("PreviousArrow");
            }

            if (nextArrowText == null)
            {
                nextArrowText = FindChildText("NextArrow");
            }

            if (previousHitTarget == null && previousArrowText != null)
            {
                previousHitTarget = previousArrowText.rectTransform;
            }

            if (nextHitTarget == null && nextArrowText != null)
            {
                nextHitTarget = nextArrowText.rectTransform;
            }
        }

        private Text FindChildText(string childName, string fallbackName = null)
        {
            var child = transform.Find(childName);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text != null || string.IsNullOrEmpty(fallbackName))
            {
                return text;
            }

            child = transform.Find(fallbackName);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static bool RectContains(RectTransform rectTransform, PointerEventData eventData)
        {
            return rectTransform != null
                && rectTransform.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position, eventData.pressEventCamera);
        }

        private bool RectContainsInlineArrow(PointerEventData eventData, out int direction)
        {
            direction = 0;
            var rectTransform = transform as RectTransform;
            if (rectTransform == null
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                return false;
            }

            var halfWidth = rectTransform.rect.width * 0.5f;
            if (localPoint.x <= -halfWidth + 56f)
            {
                direction = -1;
                return true;
            }

            if (localPoint.x >= halfWidth - 56f)
            {
                direction = 1;
                return true;
            }

            return false;
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
