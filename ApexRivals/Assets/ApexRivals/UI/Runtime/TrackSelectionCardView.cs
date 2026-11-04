using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// One track card (thumbnail + name strip + corner badge). Keyboard/gamepad focus previews its track (mouse hover does not);
    /// Submit/click selects it (the screen then moves focus to START). The selected track keeps a red frame and glow while focus is elsewhere
    /// (e.g. on the footer buttons).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackSelectionCardView : MonoBehaviour, ISelectHandler
    {
        [Header("Badge labels")]
        [SerializeField] private string lockedLabel = "LOCKED";
        [SerializeField] private string championLabel = "CHAMPION";
        [SerializeField] private string finaleLabel = "FINAL";
        [SerializeField] private string wonLabel = "WON";

        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private Button button;
        [SerializeField, Tooltip("Automatically match previews to each sprite. Disable to preserve Aspect Ratio on each Image's AspectRatioFitter.")] private bool autoFitPreviewAspect = true;
        [SerializeField] private Image thumbnail;
        [SerializeField] private GameObject thumbnailPlaceholder;
        [SerializeField] private Text nameText;
        [SerializeField] private UguiMenuButtonVisualState visualState;
        [SerializeField, Tooltip("Outline shown around the whole card while its track is selected.")] private AngledPanelGraphic selectionFrame;
        [SerializeField, Tooltip("Soft red halo behind the selected card.")] private GameObject selectionGlow;

        [Header("Corner badge (optional)")]
        [SerializeField] private GameObject badgeRoot;
        [SerializeField] private Graphic badgeFill;
        [SerializeField] private Text badgeText;

        [SerializeField, Tooltip("Disable to keep the badge RectTransform width authored in the Editor.")] private bool autoSizeBadge = true;
        [SerializeField, Min(0f)] private float badgePadding = 52f;
        [SerializeField, Min(0f)] private float minimumBadgeWidth;
        private Image _tintImage;
        private Color _authoredThumbnailColor;


        private Action<int> _focused;
        private Action<int> _submitted;

        public int Index { get; private set; } = -1;
        public Button Button => button;

        public void Initialize(int index, Action<int> focused, Action<int> submitted)
        {
            Index = index;
            _focused = focused;
            _submitted = submitted;
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
                button.onClick.AddListener(OnClicked);
            }
        }

        public void Render(TrackSelectionItemViewModel item, bool selected, bool interactable)
        {
            UguiViewText.Set(nameText, FormatName(item, selected));
            if (thumbnail != null)
            {
                if (_tintImage != thumbnail)
                {
                    _tintImage = thumbnail;
                    _authoredThumbnailColor = thumbnail.color;
                }
                thumbnail.sprite = item.Preview;
                thumbnail.enabled = item.Preview != null;
                UguiViewText.FitAspect(thumbnail, item.Preview, autoFitPreviewAspect);
                // Locked circuits stay visible but dimmed.
                thumbnail.color = _authoredThumbnailColor * (item.IsLocked ? Palette.LockedThumbnailTint : Palette.AvailableThumbnailTint);
            }

            if (thumbnailPlaceholder != null)
            {
                thumbnailPlaceholder.SetActive(item.Preview == null);
            }

            if (button != null)
            {
                button.interactable = interactable;
            }

            if (visualState != null)
            {
                visualState.IsCurrent = selected;
            }

            if (selectionFrame != null)
            {
                selectionFrame.BorderColor = selected ? Palette.FocusOutline : Palette.PanelOutline;
                selectionFrame.BorderThickness = selected ? Palette.CardBorderSelected : Palette.CardBorderResting;
            }

            if (selectionGlow != null && selectionGlow.activeSelf != selected)
            {
                selectionGlow.SetActive(selected);
            }

            RenderBadge(item);
        }

        /// <summary>
        /// The career final shows its last word in red ("APEX RIVALS FINAL"), except on the selected card
        /// whose label is already red (or white on the focused red strip).
        /// </summary>
        private string FormatName(TrackSelectionItemViewModel item, bool selected)
        {
            var name = string.IsNullOrEmpty(item.DisplayName) ? string.Empty : item.DisplayName.ToUpperInvariant();
            if (!item.IsFinale || selected)
            {
                return name;
            }

            var split = name.LastIndexOf(' ');
            return split <= 0
                ? name
                : name.Substring(0, split + 1) + "<color=" + Palette.PrimaryRedHex + ">" + name.Substring(split + 1) + "</color>";
        }

        private void RenderBadge(TrackSelectionItemViewModel item)
        {
            string label;
            Color fill;
            if (item.IsLocked)
            {
                label = lockedLabel;
                fill = Palette.Disabled;
            }
            else if (item.IsFinale)
            {
                label = item.StatusText == "CHAMPION" ? championLabel : finaleLabel;
                fill = item.StatusText == "CHAMPION" ? Palette.SuccessGreen : Palette.PrimaryRed;
            }
            else if (item.StatusText == "WON")
            {
                label = wonLabel;
                fill = Palette.SuccessGreen;
            }
            else if (!string.IsNullOrEmpty(item.StatusText))
            {
                label = item.StatusText;
                fill = Palette.PanelElevated;
            }
            else
            {
                label = string.Empty;
                fill = Color.clear;
            }

            var show = !string.IsNullOrEmpty(label);
            if (badgeRoot != null && badgeRoot.activeSelf != show)
            {
                badgeRoot.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            UguiViewText.Set(badgeText, label);
            if (badgeFill != null)
            {
                badgeFill.color = fill;
            }

            // Size the badge to its label ("WON" vs "CHAMPION"); the slanted left edge needs the extra room.
            if (autoSizeBadge && badgeRoot != null && badgeText != null && badgeRoot.transform is RectTransform badgeRect)
            {
                var size = badgeRect.sizeDelta;
                size.x = Mathf.Max(minimumBadgeWidth, Mathf.Ceil(badgeText.preferredWidth) + badgePadding);
                badgeRect.sizeDelta = size;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            // Keyboard/gamepad focus previews the track; a mouse only changes it with a click (hover just highlights).
            if (UiInputModality.IsMouse)
            {
                return;
            }

            _focused?.Invoke(Index);
        }

        private void OnClicked()
        {
            _submitted?.Invoke(Index);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }
        }
    }
}
