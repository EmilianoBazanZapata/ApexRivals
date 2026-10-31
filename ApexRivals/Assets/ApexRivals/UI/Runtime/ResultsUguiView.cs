using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Results screen. The RACE COMPLETE layout shows the place, race time and best lap, the classification table,
    /// the rewards and CONTINUE (back to Track Selection) / RETRY. Older layouts (position line, garage and main menu
    /// buttons) still render through their optional fields.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultsUguiView : MonoBehaviour, IResultsView, ICancelHandler
    {
        [Header("Dynamic text templates")]
        [SerializeField] private string positionFormat = "POSITION {0}/{1}";
        [SerializeField] private string rewardFormat = "REWARD: {0}";
        [SerializeField] private string currencyFormat = "CURRENCY: {0}";
        [SerializeField] private string completedRacesFormat = "Completed races: {0}";
        [SerializeField] private string tierFormat = "Tier: {0}";
        [SerializeField] private string newTierFormat = "Completed races: {0} | New tier: {1}";
        [SerializeField] private string progressionFormat = "Completed races: {0} | {1}";

        [Header("Display text")]
        [SerializeField] private string careerFallbackTitle = "CAREER";
        [SerializeField] private string noBestLapLabel = "--:--.---";
        [SerializeField] private string retryError = "COULD NOT RESTART THE RACE";
        [SerializeField] private string continueError = "COULD NOT OPEN TRACK SELECTION";
        [SerializeField] private string returnGarageError = "COULD NOT RETURN TO THE GARAGE";
        [SerializeField] private string returnMainMenuError = "COULD NOT RETURN TO THE MAIN MENU";

        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private Text finalPositionText;
        [SerializeField] private Text raceTimeText;
        [SerializeField] private Text rewardText;
        [SerializeField] private Text updatedCurrencyText;
        [SerializeField] private Text progressionText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button returnToGarageButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private Text statusText;

        [Header("Career (optional)")]
        [SerializeField] private Text careerTitleText;
        [SerializeField] private Text careerBodyText;
        [SerializeField, Tooltip("Career completion after the race.")] private ChevronProgressBar careerProgressBar;

        [Header("Race complete layout (optional)")]
        [SerializeField, Tooltip("Back to Track Selection.")] private Button continueButton;
        [SerializeField, Tooltip("Place number (\"1\").")] private Text placeNumberText;
        [SerializeField, Tooltip("Ordinal suffix (\"ST\").")] private Text placeSuffixText;
        [SerializeField] private Text bestLapText;
        [SerializeField] private GameObject[] classificationRows;
        [SerializeField] private Graphic[] classificationRowBackgrounds;
        [SerializeField] private Text[] classificationPositionTexts;
        [SerializeField] private Text[] classificationNameTexts;
        [SerializeField] private Text[] classificationTimeTexts;
        [SerializeField] private Text[] classificationBestLapTexts;

        private readonly List<RaceClassificationEntry> _classification = new List<RaceClassificationEntry>();
        private ResultsPresenter _presenter;
        private RaceStandingsTracker _timing;
        private bool _subscribed;

        public void Bind(ResultsPresenter presenter)
        {
            _presenter = presenter;
            if (presenter == null) Unsubscribe();
            else Subscribe();
        }

        /// <summary>Lap times and classification of the session; pass null on teardown.</summary>
        public void BindTiming(RaceStandingsTracker timing)
        {
            _timing = timing;
        }

        public void Render(ResultsViewModel viewModel)
        {
            // Upper case like every other screen.
            UguiViewText.Set(finalPositionText, UguiViewText.FormatTemplate(positionFormat, "POSITION {0}/{1}", viewModel.FinalPosition, viewModel.ParticipantCount));
            UguiViewText.Set(raceTimeText, UguiViewText.FormatTime(viewModel.RaceTime));
            UguiViewText.Set(rewardText, placeNumberText != null
                ? "+" + viewModel.EarnedReward.ToString("N0", CultureInfo.InvariantCulture)
                : UguiViewText.FormatTemplate(rewardFormat, "REWARD: {0}", viewModel.EarnedReward));
            UguiViewText.Set(updatedCurrencyText, UguiViewText.FormatTemplate(currencyFormat, "CURRENCY: {0}", viewModel.UpdatedCurrency));
            UguiViewText.Set(progressionText, CreateProgressionText(viewModel).ToUpperInvariant());
            UguiViewText.Set(statusText, StatusText(viewModel.MessageKey));
            UguiViewText.Set(careerTitleText, string.IsNullOrEmpty(viewModel.CareerTitle) && placeNumberText != null ? careerFallbackTitle : viewModel.CareerTitle);
            UguiViewText.Set(careerBodyText, placeNumberText != null ? viewModel.CareerBody.ToUpperInvariant() : viewModel.CareerBody);
            if (careerTitleText != null)
            {
                careerTitleText.color = viewModel.CareerCompleted ? Palette.AccentText : Palette.PrimaryText;
            }

            if (careerProgressBar != null)
            {
                careerProgressBar.gameObject.SetActive(viewModel.CareerPercent >= 0);
                careerProgressBar.Value = Mathf.Max(0, viewModel.CareerPercent) / 100f;
            }

            UguiViewText.Set(placeNumberText, viewModel.FinalPosition.ToString(CultureInfo.InvariantCulture));
            UguiViewText.Set(placeSuffixText, RaceHudUguiView.OrdinalSuffix(viewModel.FinalPosition));
            RenderTiming();

            SetButton(continueButton, !viewModel.IsTransitioning);
            SetButton(retryButton, !viewModel.IsTransitioning);
            SetButton(returnToGarageButton, !viewModel.IsTransitioning);
            SetButton(returnToMainMenuButton, !viewModel.IsTransitioning);
        }

        private void RenderTiming()
        {
            if (_timing == null)
            {
                return;
            }

            var best = _timing.BestLapTime;
            UguiViewText.Set(bestLapText, best < 0f ? noBestLapLabel : UguiViewText.FormatTime(best));
            if (classificationRows == null)
            {
                return;
            }

            _timing.GetClassification(_classification);
            for (var row = 0; row < classificationRows.Length; row++)
            {
                var visible = row < _classification.Count;
                if (classificationRows[row] != null && classificationRows[row].activeSelf != visible)
                {
                    classificationRows[row].SetActive(visible);
                }

                if (!visible) continue;
                var entry = _classification[row];
                UguiViewText.Set(At(classificationPositionTexts, row), entry.Position.ToString(CultureInfo.InvariantCulture));
                UguiViewText.Set(At(classificationNameTexts, row), entry.Name);
                UguiViewText.Set(At(classificationTimeTexts, row), entry.Time);
                UguiViewText.Set(At(classificationBestLapTexts, row), entry.BestLap);
                var background = At(classificationRowBackgrounds, row);
                if (background != null)
                {
                    background.color = entry.IsPlayer ? Palette.PrimaryRedIdle : Palette.Panel;
                }
            }
        }

        private static T At<T>(T[] items, int index) where T : class
        {
            return items != null && index < items.Length ? items[index] : null;
        }

        private void OnEnable()
        {
            Subscribe();
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

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
            }

            if (returnToGarageButton != null)
            {
                returnToGarageButton.onClick.AddListener(OnReturnToGarageClicked);
            }

            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (returnToGarageButton != null)
            {
                returnToGarageButton.onClick.RemoveListener(OnReturnToGarageClicked);
            }

            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);
            }

            _subscribed = false;
        }

        /// <summary>
        /// Esc/B never leaves the results screen on its own: it moves focus to the way out (CONTINUE, or "Return to
        /// garage" in older layouts) and the player confirms with Submit.
        /// </summary>
        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            UiFocus.Request(continueButton != null ? continueButton : returnToGarageButton);
        }

        private async void OnContinueClicked()
        {
            if (_presenter != null)
            {
                await _presenter.Continue();
            }
        }

        private async void OnRetryClicked()
        {
            if (_presenter != null)
            {
                await _presenter.Retry();
            }
        }

        private async void OnReturnToGarageClicked()
        {
            if (_presenter != null)
            {
                await _presenter.ReturnToGarage();
            }
        }

        private async void OnReturnToMainMenuClicked()
        {
            if (_presenter != null)
            {
                await _presenter.ReturnToMainMenu();
            }
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        /// <summary>Player-facing error line; raw message keys never reach the screen.</summary>
        private string StatusText(string messageKey)
        {
            return messageKey switch
            {
                "ui.results.retryFailed" => retryError,
                "ui.results.continueFailed" => continueError,
                "ui.results.returnGarageFailed" => returnGarageError,
                "ui.results.returnMainMenuFailed" => returnMainMenuError,
                _ => string.Empty
            };
        }

        private string CreateProgressionText(ResultsViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.CurrentTierId))
            {
                return UguiViewText.FormatTemplate(completedRacesFormat, "Completed races: {0}", viewModel.CompletedRaceCount);
            }

            var tierText = UguiViewText.FormatTemplate(tierFormat, "Tier: {0}", viewModel.CurrentTierDisplayName);
            return viewModel.NewTierReached
                ? UguiViewText.FormatTemplate(newTierFormat, "Completed races: {0} | New tier: {1}", viewModel.CompletedRaceCount, viewModel.CurrentTierDisplayName)
                : UguiViewText.FormatTemplate(progressionFormat, "Completed races: {0} | {1}", viewModel.CompletedRaceCount, tierText);
        }
    }
}
