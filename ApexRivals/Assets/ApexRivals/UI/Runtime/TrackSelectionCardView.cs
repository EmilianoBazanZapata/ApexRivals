using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// One carousel card (thumbnail + name strip). Focusing a card previews its track; Submit/click races it.
    /// The selected track keeps the "current" look while focus is elsewhere (e.g. on the footer buttons).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackSelectionCardView : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image thumbnail;
        [SerializeField] private GameObject thumbnailPlaceholder;
        [SerializeField] private Text nameText;
        [SerializeField] private UguiMenuButtonVisualState visualState;
        [SerializeField, Tooltip("Outline shown around the whole card while its track is selected.")] private AngledPanelGraphic selectionFrame;

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
            UguiViewText.Set(nameText, string.IsNullOrEmpty(item.DisplayName) ? string.Empty : item.DisplayName.ToUpperInvariant());
            if (thumbnail != null)
            {
                thumbnail.sprite = item.Preview;
                thumbnail.enabled = item.Preview != null;
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
                selectionFrame.BorderColor = selected ? ApexUiVisualStyle.FocusOutline : ApexUiVisualStyle.PanelOutline;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
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
