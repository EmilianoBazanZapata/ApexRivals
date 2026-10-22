using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ResultsUguiView : MonoBehaviour, IResultsView, ICancelHandler
    {
        [SerializeField] private Text finalPositionText;
        [SerializeField] private Text raceTimeText;
        [SerializeField] private Text rewardText;
        [SerializeField] private Text updatedCurrencyText;
        [SerializeField] private Text progressionText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button returnToGarageButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private Text statusText;

        private ResultsPresenter _presenter;
        private bool _subscribed;

        public void Bind(ResultsPresenter presenter)
        {
            _presenter = presenter;
            if (presenter == null) Unsubscribe();
            else Subscribe();
        }

        public void Render(ResultsViewModel viewModel)
        {
            UguiViewText.Set(finalPositionText, $"Position {viewModel.FinalPosition}/{viewModel.ParticipantCount}");
            UguiViewText.Set(raceTimeText, UguiViewText.FormatTime(viewModel.RaceTime));
            UguiViewText.Set(rewardText, $"Reward: {viewModel.EarnedReward}");
            UguiViewText.Set(updatedCurrencyText, $"Currency: {viewModel.UpdatedCurrency}");
            UguiViewText.Set(progressionText, CreateProgressionText(viewModel));
            UguiViewText.Set(statusText, viewModel.MessageKey);
            SetButton(retryButton, !viewModel.IsTransitioning);
            SetButton(returnToGarageButton, !viewModel.IsTransitioning);
            SetButton(returnToMainMenuButton, !viewModel.IsTransitioning);
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
        /// Esc/B never leaves the results screen on its own: it moves focus to "Return to garage" and the
        /// player confirms with Submit. Pending design decision; change the target here if needed.
        /// </summary>
        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            UiFocus.Request(returnToGarageButton);
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

        private static string CreateProgressionText(ResultsViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.CurrentTierId))
            {
                return $"Completed races: {viewModel.CompletedRaceCount}";
            }

            var tierText = $"Tier: {viewModel.CurrentTierDisplayName}";
            return viewModel.NewTierReached
                ? $"Completed races: {viewModel.CompletedRaceCount} | New tier: {viewModel.CurrentTierDisplayName}"
                : $"Completed races: {viewModel.CompletedRaceCount} | {tierText}";
        }
    }
}
