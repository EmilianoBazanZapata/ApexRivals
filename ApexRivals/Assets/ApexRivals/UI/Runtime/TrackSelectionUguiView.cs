using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Race Select screen: large preview + details/minimap for the selected track and a card carousel.
    /// Left/right moves between cards (focus previews the track), Q/E or LB/RB cycle tracks, Submit on a card
    /// selects it and moves focus to START, START races the selected track and Cancel returns to the garage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackSelectionUguiView : MonoBehaviour, ITrackSelectionView, IUiTabNavigation, ICancelHandler
    {
        [SerializeField, Tooltip("Disable to preserve Navigation mode, links and wrapping authored on the Selectable components. Enable to rebuild links as controls appear/disappear.")] private bool useDynamicNavigation = true;

        [Header("Display text")]
        [SerializeField] private string noTracksLabel = "NO TRACKS AVAILABLE";
        [SerializeField] private string lockedPrefix = "LOCKED - ";
        [SerializeField] private string finalEventLabel = "FINAL EVENT";
        [SerializeField] private string startRaceError = "COULD NOT START THE RACE";
        [SerializeField] private string noTrackError = "NO TRACK SELECTED";
        [SerializeField] private string returnMainMenuError = "COULD NOT RETURN TO THE MAIN MENU";
        [SerializeField] private string careerProgressFormat = "CAREER  <color={0}>{1}%</color>";

        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField, Tooltip("Automatically match previews to each sprite. Disable to preserve Aspect Ratio on each Image's AspectRatioFitter.")] private bool autoFitPreviewAspect = true;
        [SerializeField] private Image previewImage;
        [SerializeField] private GameObject previewPlaceholder;

        [SerializeField] private Text trackNameText;
        [SerializeField] private Text locationText;
        [SerializeField] private Text lengthValueText;
        [SerializeField] private Text lapsValueText;
        [SerializeField] private Text environmentValueText;
        [SerializeField] private Text surfaceValueText;
        [SerializeField] private TrackMinimapGraphic minimap;

        [SerializeField] private RectTransform cardContainer;
        [SerializeField, Tooltip("Inactive template cloned once per track.")] private TrackSelectionCardView cardTemplate;
        [SerializeField, Min(1)] private int visibleCardCount = 5;
        [SerializeField] private Button previousTrackButton;
        [SerializeField] private Button nextTrackButton;

        [SerializeField] private Button backButton;
        [SerializeField] private Button startRaceButton;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject feedbackRoot;

        [Header("Career (optional)")]
        [SerializeField, Tooltip("Lock requirement or race status of the selected entry.")] private Text requirementText;
        [SerializeField, Tooltip("Career completion and next objective.")] private Text objectiveText;
        [SerializeField, Tooltip("Header career box; hidden without a career.")] private GameObject careerRoot;
        [SerializeField, Tooltip("\"CAREER 68%\" (the percent is drawn in red).")] private Text careerTitleText;
        [SerializeField, Tooltip("Bar fill; its anchorMax.x is set to the completion ratio.")] private RectTransform careerProgressFill;
        [SerializeField, Tooltip("\"NEXT: ...\" or \"CAREER COMPLETE\".")] private Text careerNextText;
        [SerializeField, Tooltip("Chevron progress row; replaces the plain fill when assigned.")] private ChevronProgressBar careerProgressBar;
        [SerializeField, Tooltip("\"68%\" beside a fixed CAREER PROGRESS title; without it the title carries the percent.")] private Text careerPercentText;

        private readonly List<TrackSelectionCardView> _cards = new List<TrackSelectionCardView>();
        private TrackSelectionPresenter _presenter;
        private bool _subscribed;
        private int _windowStart;
        private bool _focusSelectedCard;

        public void Bind(TrackSelectionPresenter presenter)
        {
            _presenter = presenter;
            if (presenter == null) Unsubscribe();
            else Subscribe();
        }

        public void Render(TrackSelectionViewModel viewModel)
        {
            if (this == null)
            {
                return;
            }

            var focusWasOnCard = FocusedCard() != null;
            RenderDetails(viewModel);
            RenderCards(viewModel);

            var canCycle = !viewModel.OperationActive && viewModel.Tracks.Count > 1;
            SetButton(previousTrackButton, canCycle);
            SetButton(nextTrackButton, canCycle);
            SetButton(startRaceButton, viewModel.CanStartRace);
            SetButton(backButton, !viewModel.OperationActive);

            UguiViewText.Set(objectiveText, viewModel.ObjectiveText);
            RenderCareer(viewModel);
            var feedback = string.IsNullOrEmpty(viewModel.Feedback) ? FormatStatus(viewModel.MessageKey) : viewModel.Feedback.ToUpperInvariant();
            UguiViewText.Set(statusText, feedback);
            if (feedbackRoot != null)
            {
                feedbackRoot.SetActive(!string.IsNullOrEmpty(feedback));
            }

            ConfigureNavigation(viewModel.SelectedIndex);
            if (focusWasOnCard || _focusSelectedCard)
            {
                // Q/E and the arrows move the selection; keep keyboard/gamepad focus on the matching card.
                var selectedCard = CardAt(viewModel.SelectedIndex);
                if (selectedCard != null && FocusedCard() != selectedCard)
                {
                    UiFocus.Request(selectedCard.Button);
                }
            }

            EnsureFocus(viewModel.SelectedIndex);
        }

        public void ShowPreviousTab()
        {
            _presenter?.SelectPreviousTrack();
        }

        public void ShowNextTab()
        {
            _presenter?.SelectNextTrack();
        }

        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            OnBackClicked();
        }

        private void RenderDetails(TrackSelectionViewModel viewModel)
        {
            if (!viewModel.HasSelection)
            {
                UguiViewText.Set(trackNameText, noTracksLabel);
                UguiViewText.Set(requirementText, string.Empty);
                UguiViewText.Set(locationText, string.Empty);
                UguiViewText.Set(lengthValueText, "-");
                UguiViewText.Set(lapsValueText, "-");
                UguiViewText.Set(environmentValueText, "-");
                UguiViewText.Set(surfaceValueText, "-");
                SetPreview(null);
                if (minimap != null) minimap.SetPoints(null);
                return;
            }

            var track = viewModel.Selected;
            UguiViewText.Set(trackNameText, TwoToneTitle(Upper(track.DisplayName)));
            // Race status (WON, CHAMPION...) lives on the card badge; this line only explains locks and the final.
            UguiViewText.Set(requirementText, track.IsLocked ? lockedPrefix + track.LockText : track.IsFinale ? FinaleLine(track) : string.Empty);
            if (requirementText != null)
            {
                requirementText.color = track.IsLocked || track.IsFinale ? Palette.AccentText : Palette.SecondaryText;
            }

            UguiViewText.Set(locationText, Upper(track.Location));
            UguiViewText.Set(lengthValueText, track.LengthText);
            UguiViewText.Set(lapsValueText, track.Laps.ToString(CultureInfo.InvariantCulture));
            UguiViewText.Set(environmentValueText, Upper(track.Environment));
            UguiViewText.Set(surfaceValueText, Upper(track.Surface));
            SetPreview(track.Preview);
            if (minimap != null) minimap.SetPoints(track.MinimapPoints);
        }

        private void RenderCareer(TrackSelectionViewModel viewModel)
        {
            if (careerRoot != null && careerRoot.activeSelf != viewModel.HasCareer)
            {
                careerRoot.SetActive(viewModel.HasCareer);
            }

            if (!viewModel.HasCareer)
            {
                return;
            }

            if (careerPercentText != null)
            {
                UguiViewText.Set(careerPercentText, viewModel.CareerPercent.ToString(CultureInfo.InvariantCulture) + "%");
            }
            else
            {
                UguiViewText.Set(careerTitleText, UguiViewText.FormatTemplate(
                    careerProgressFormat, "CAREER  <color={0}>{1}%</color>",
                    Palette.PrimaryRedHex,
                    viewModel.CareerPercent));
            }

            UguiViewText.Set(careerNextText, viewModel.CareerNextText);
            if (careerProgressBar != null)
            {
                careerProgressBar.Value = viewModel.CareerPercent / 100f;
            }

            if (careerProgressFill != null)
            {
                var max = careerProgressFill.anchorMax;
                max.x = viewModel.CareerPercent / 100f;
                careerProgressFill.anchorMax = max;
            }
        }

        /// <summary>The final's goal ("FINISH 1ST AGAINST ...") plus "CHAMPION" once won; the subtitle names the circuit.</summary>
        private string FinaleLine(TrackSelectionItemViewModel track)
        {
            var goal = string.IsNullOrEmpty(track.Goal) ? finalEventLabel : Upper(track.Goal);
            return string.IsNullOrEmpty(track.StatusText) ? goal : goal + " - " + track.StatusText;
        }

        /// <summary>"APEX CIRCUIT" → first word in the primary text colour, the rest in red.</summary>
        private string TwoToneTitle(string title)
        {
            var split = string.IsNullOrEmpty(title) ? -1 : title.IndexOf(' ');
            return split <= 0
                ? title
                : title.Substring(0, split) + " <color=" + Palette.PrimaryRedHex + ">" + title.Substring(split + 1) + "</color>";
        }

        private void SetPreview(Sprite sprite)
        {
            if (previewImage != null)
            {
                previewImage.sprite = sprite;
                previewImage.enabled = sprite != null;
                UguiViewText.FitAspect(previewImage, sprite, autoFitPreviewAspect);
            }

            if (previewPlaceholder != null)
            {
                previewPlaceholder.SetActive(sprite == null);
            }
        }

        private void RenderCards(TrackSelectionViewModel viewModel)
        {
            var tracks = viewModel.Tracks;
            EnsureCardCount(tracks.Count);

            // Slide the visible window just enough to keep the selected card on screen.
            var visible = Mathf.Max(1, visibleCardCount);
            if (viewModel.SelectedIndex >= 0)
            {
                if (viewModel.SelectedIndex < _windowStart) _windowStart = viewModel.SelectedIndex;
                else if (viewModel.SelectedIndex >= _windowStart + visible) _windowStart = viewModel.SelectedIndex - visible + 1;
            }

            _windowStart = Mathf.Clamp(_windowStart, 0, Mathf.Max(0, tracks.Count - visible));
            for (var index = 0; index < _cards.Count; index++)
            {
                var card = _cards[index];
                var shown = index < tracks.Count && index >= _windowStart && index < _windowStart + visible;
                if (card.gameObject.activeSelf != shown)
                {
                    card.gameObject.SetActive(shown);
                }

                if (index < tracks.Count)
                {
                    card.Render(tracks[index], index == viewModel.SelectedIndex, !viewModel.OperationActive);
                }
            }
        }

        private void EnsureCardCount(int count)
        {
            if (cardTemplate == null || cardContainer == null)
            {
                return;
            }

            if (cardTemplate.gameObject.activeSelf)
            {
                cardTemplate.gameObject.SetActive(false);
            }

            while (_cards.Count < count)
            {
                var card = Instantiate(cardTemplate, cardContainer);
                card.name = $"TrackCard_{_cards.Count}";
                card.Initialize(_cards.Count, OnCardFocused, OnCardSubmitted);
                card.gameObject.SetActive(true);
                _cards.Add(card);
            }

            for (var index = _cards.Count - 1; index >= count; index--)
            {
                Destroy(_cards[index].gameObject);
                _cards.RemoveAt(index);
            }
        }

        /// <summary>
        /// Cards form a left/right row; Down goes to the footer (Start Race first), Up from the footer
        /// returns to the selected card. The carousel arrows are pointer-only so focus never parks on them.
        /// </summary>
        private void ConfigureNavigation(int selectedIndex)
        {
            if (!useDynamicNavigation) return;
            Selectable firstCard = null;
            Selectable lastCard = null;
            TrackSelectionCardView previous = null;
            for (var index = 0; index < _cards.Count; index++)
            {
                var card = _cards[index];
                if (!card.gameObject.activeSelf)
                {
                    continue;
                }

                firstCard ??= card.Button;
                lastCard = card.Button;
                var next = NextActiveCard(index);
                UiNavigation.Set(
                    card.Button,
                    null,
                    UiNavigation.First(startRaceButton, backButton),
                    previous != null ? previous.Button : null,
                    next != null ? next.Button : null);
                previous = card;
            }

            var selectedCard = CardAt(selectedIndex);
            var cardTarget = selectedCard != null && selectedCard.gameObject.activeSelf ? selectedCard.Button : UiNavigation.First(firstCard, lastCard);
            UiNavigation.Set(backButton, cardTarget, null, null, UiNavigation.First(startRaceButton));
            UiNavigation.Set(startRaceButton, cardTarget, null, UiNavigation.First(backButton), null);
            DisableNavigation(previousTrackButton);
            DisableNavigation(nextTrackButton);
        }

        private TrackSelectionCardView NextActiveCard(int index)
        {
            for (var next = index + 1; next < _cards.Count; next++)
            {
                if (_cards[next].gameObject.activeSelf)
                {
                    return _cards[next];
                }
            }

            return null;
        }

        private void EnsureFocus(int selectedIndex)
        {
            if (UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)))
            {
                return;
            }

            var selectedCard = CardAt(selectedIndex);
            UiFocus.Request(UiNavigation.First(selectedCard != null ? selectedCard.Button : null, startRaceButton, backButton));
        }

        private TrackSelectionCardView FocusedCard()
        {
            var selected = UiFocus.CurrentSelection;
            if (selected == null)
            {
                return null;
            }

            for (var index = 0; index < _cards.Count; index++)
            {
                if (_cards[index].gameObject == selected)
                {
                    return _cards[index];
                }
            }

            return null;
        }

        private TrackSelectionCardView CardAt(int index)
        {
            return index >= 0 && index < _cards.Count ? _cards[index] : null;
        }

        private void OnCardFocused(int index)
        {
            _presenter?.SelectTrack(index);
        }

        /// <summary>Click / Submit on a card picks that track and hands focus to START; only START begins the race.</summary>
        private void OnCardSubmitted(int index)
        {
            if (_presenter == null)
            {
                return;
            }

            _presenter.SelectTrack(index);
            if (startRaceButton != null && startRaceButton.IsInteractable())
            {
                UiFocus.Request(startRaceButton);
            }
        }

        private void OnEnable()
        {
            // Entering the screen focuses the selected card. Held until the end of the frame because the
            // screen navigator applies its own initial selection after this panel is enabled.
            _focusSelectedCard = true;
            Subscribe();
            _presenter?.Present();
        }

        private void LateUpdate()
        {
            _focusSelectedCard = false;
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (previousTrackButton != null) previousTrackButton.onClick.AddListener(ShowPreviousTab);
            if (nextTrackButton != null) nextTrackButton.onClick.AddListener(ShowNextTab);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            if (startRaceButton != null) startRaceButton.onClick.AddListener(OnStartRaceClicked);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (previousTrackButton != null) previousTrackButton.onClick.RemoveListener(ShowPreviousTab);
            if (nextTrackButton != null) nextTrackButton.onClick.RemoveListener(ShowNextTab);
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
            if (startRaceButton != null) startRaceButton.onClick.RemoveListener(OnStartRaceClicked);
            _subscribed = false;
        }

        private async void OnBackClicked()
        {
            if (_presenter != null)
            {
                await _presenter.Back();
            }
        }

        private async void OnStartRaceClicked()
        {
            if (_presenter != null)
            {
                await _presenter.StartRace();
            }
        }

        private string FormatStatus(string messageKey)
        {
            return messageKey switch
            {
                "ui.trackSelect.startRaceFailed" => startRaceError,
                "ui.trackSelect.noTrack" => noTrackError,
                "ui.trackSelect.backFailed" => returnMainMenuError,
                _ => string.Empty
            };
        }

        private static string Upper(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToUpperInvariant();
        }

        private static void DisableNavigation(Selectable selectable)
        {
            if (selectable != null)
            {
                selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
