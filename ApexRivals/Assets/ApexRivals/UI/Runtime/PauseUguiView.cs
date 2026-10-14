using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PauseUguiView : MonoBehaviour, IPauseView, ICancelHandler
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject confirmationDialogRoot;
        [SerializeField] private Text confirmationText;
        [SerializeField] private Button confirmationConfirmButton;
        [SerializeField] private Button confirmationCancelButton;

        private PausePresenter _presenter;
        private bool _subscribed;
        private Action _pendingConfirmedAction;
        private bool _wasPaused;
        private bool _confirmationSubmitting;

        public event Action ResumeRequested;

        public void Bind(PausePresenter presenter)
        {
            _presenter = presenter;
            Subscribe();
        }

        public void Render(PauseViewModel viewModel)
        {
            if (!viewModel.IsPaused && _wasPaused)
            {
                ExitPauseView();
            }

            gameObject.SetActive(viewModel.IsPaused);
            SetButton(resumeButton, viewModel.CanResume);
            SetButton(retryButton, viewModel.CanRetry);
            SetButton(returnToMainMenuButton, viewModel.IsPaused && !viewModel.IsTransitioning);
            ConfigurePauseNavigation();
            UguiViewText.Set(statusText, viewModel.MessageKey);

            if (viewModel.IsPaused && !_wasPaused)
            {
                EnterPauseView();
            }

            if (!viewModel.IsPaused)
            {
                HideConfirmation();
            }

            _wasPaused = viewModel.IsPaused;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_wasPaused)
            {
                ExitPauseView();
                _wasPaused = false;
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
            }

            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);
            }

            if (confirmationConfirmButton != null)
            {
                confirmationConfirmButton.onClick.AddListener(OnConfirmationConfirmed);
            }

            if (confirmationCancelButton != null)
            {
                confirmationCancelButton.onClick.AddListener(OnConfirmationCanceled);
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(OnResumeClicked);
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);
            }

            if (confirmationConfirmButton != null)
            {
                confirmationConfirmButton.onClick.RemoveListener(OnConfirmationConfirmed);
            }

            if (confirmationCancelButton != null)
            {
                confirmationCancelButton.onClick.RemoveListener(OnConfirmationCanceled);
            }

            _subscribed = false;
        }

        private void OnResumeClicked()
        {
            ResumeRequested?.Invoke();
            _presenter?.Resume();
        }

        private void OnRetryClicked()
        {
            RequestConfirmation("Restart the current race?", RunRetry);
        }

        private void OnReturnToMainMenuClicked()
        {
            RequestConfirmation("Exit to the main menu? Race progress will be lost.", RunReturnToMainMenu);
        }

        private async void RunRetry()
        {
            if (_presenter != null)
            {
                await Run(_presenter.Retry());
            }
        }

        private async void RunReturnToMainMenu()
        {
            if (_presenter != null)
            {
                await Run(_presenter.ReturnToMainMenu());
            }
        }

        private static async Task Run(Task task)
        {
            await task;
        }

        private void RequestConfirmation(string message, Action onConfirmed)
        {
            if (_confirmationSubmitting)
            {
                return;
            }

            if (confirmationDialogRoot == null)
            {
                onConfirmed();
                return;
            }

            _pendingConfirmedAction = onConfirmed;
            UguiViewText.Set(confirmationText, message);
            UiModalPointerBlocker.Ensure(confirmationDialogRoot);
            confirmationDialogRoot.SetActive(true);
            ConfigureConfirmationNavigation();

            // The dialog is a modal focus scope: navigation, hover and repairs stay inside it, and
            // closing it gives focus back to the button that opened it.
            UiFocus.PushScope(confirmationDialogRoot, confirmationConfirmButton);
        }

        private void OnConfirmationConfirmed()
        {
            if (_confirmationSubmitting)
            {
                return;
            }

            _confirmationSubmitting = true;
            SetConfirmationButtons(false);
            var action = _pendingConfirmedAction;
            HideConfirmation();
            action?.Invoke();
        }

        private void OnConfirmationCanceled()
        {
            if (_confirmationSubmitting)
            {
                return;
            }

            HideConfirmation();
        }

        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            if (confirmationDialogRoot != null && confirmationDialogRoot.activeInHierarchy)
            {
                OnConfirmationCanceled();
                return;
            }

            OnResumeClicked();
        }

        private void HideConfirmation()
        {
            _pendingConfirmedAction = null;
            if (confirmationDialogRoot != null)
            {
                // Pop while the dialog is still active so the previous focus is restored, not repaired.
                UiFocus.PopScope(confirmationDialogRoot);
                confirmationDialogRoot.SetActive(false);
            }

            SetConfirmationButtons(true);
            _confirmationSubmitting = false;
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void EnterPauseView()
        {
            HideConfirmation();

            // UguiScreenNavigator owns screen focus and restores it on resume; this only covers the case
            // where the pause buttons become active after the navigator already switched screens.
            UiFocus.Request(GetFirstPauseSelectable());
        }

        private void ExitPauseView()
        {
            HideConfirmation();
        }

        private void SetConfirmationButtons(bool interactable)
        {
            SetButton(confirmationConfirmButton, interactable);
            SetButton(confirmationCancelButton, interactable);
        }

        private void ConfigureConfirmationNavigation()
        {
            if (confirmationConfirmButton == null || confirmationCancelButton == null)
            {
                return;
            }

            ConfigureTwoWayNavigation(confirmationConfirmButton, confirmationCancelButton);
            ConfigureTwoWayNavigation(confirmationCancelButton, confirmationConfirmButton);
        }

        private static void ConfigureTwoWayNavigation(Button button, Button other)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = other;
            navigation.selectOnDown = other;
            navigation.selectOnLeft = other;
            navigation.selectOnRight = other;
            button.navigation = navigation;
        }

        private void ConfigurePauseNavigation()
        {
            var buttons = new List<Button>(3);
            AddNavigableButton(buttons, resumeButton);
            AddNavigableButton(buttons, retryButton);
            AddNavigableButton(buttons, returnToMainMenuButton);

            for (var index = 0; index < buttons.Count; index++)
            {
                var navigation = buttons[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = buttons[(index + buttons.Count - 1) % buttons.Count];
                navigation.selectOnDown = buttons[(index + 1) % buttons.Count];
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                buttons[index].navigation = navigation;
            }
        }

        private GameObject GetFirstPauseSelectable()
        {
            if (resumeButton != null && resumeButton.IsInteractable())
            {
                return resumeButton.gameObject;
            }

            if (retryButton != null && retryButton.IsInteractable())
            {
                return retryButton.gameObject;
            }

            return returnToMainMenuButton != null && returnToMainMenuButton.IsInteractable()
                ? returnToMainMenuButton.gameObject
                : null;
        }

        private static void AddNavigableButton(ICollection<Button> buttons, Button button)
        {
            if (button != null && button.gameObject.activeSelf && button.IsInteractable())
            {
                buttons.Add(button);
            }
        }
    }
}
