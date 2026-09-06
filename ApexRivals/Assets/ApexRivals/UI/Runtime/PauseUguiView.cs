using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PauseUguiView : MonoBehaviour, IPauseView
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private Text statusText;

        private PausePresenter _presenter;
        private bool _subscribed;

        public void Bind(PausePresenter presenter)
        {
            _presenter = presenter;
            Subscribe();
        }

        public void Render(PauseViewModel viewModel)
        {
            gameObject.SetActive(viewModel.IsPaused);
            SetButton(resumeButton, viewModel.CanResume);
            SetButton(retryButton, viewModel.CanRetry);
            SetButton(returnToMainMenuButton, viewModel.CanReturnToMainMenu);
            UguiViewText.Set(statusText, viewModel.MessageKey);
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

            _subscribed = false;
        }

        private void OnResumeClicked()
        {
            _presenter?.Resume();
        }

        private async void OnRetryClicked()
        {
            if (_presenter != null)
            {
                await Run(_presenter.Retry());
            }
        }

        private async void OnReturnToMainMenuClicked()
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

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
