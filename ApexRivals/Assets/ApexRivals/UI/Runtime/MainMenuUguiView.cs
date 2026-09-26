using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MainMenuUguiView : MonoBehaviour, IMainMenuView
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text startButtonPrimaryText;
        [SerializeField] private Text startButtonDescriptionText;
        [SerializeField] private Text garageButtonPrimaryText;
        [SerializeField] private Text garageButtonDescriptionText;
        [SerializeField] private Text settingsButtonPrimaryText;
        [SerializeField] private Text settingsButtonDescriptionText;
        [SerializeField] private Text quitButtonPrimaryText;
        [SerializeField] private Text quitButtonDescriptionText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button garageButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private UguiMainMenuNavigationController navigationController;
        [SerializeField] private GameObject loadingBlocker;
        [SerializeField] private Text errorText;

        private MainMenuPresenter _presenter;
        private MainMenuViewModel _current;
        private bool _subscribed;

        public void Bind(MainMenuPresenter presenter)
        {
            _presenter = presenter;
            Subscribe();
        }

        public void Render(MainMenuViewModel viewModel)
        {
            _current = viewModel;
            // titleText points at the "APEX" half of the stylized ApexRivalsLogo
            // (ApexMark + APEXText + RIVALSText). It is static branding art, not
            // view-model data - writing the full "Apex Rivals" string into it here
            // used to duplicate "RIVALS" beneath the separate RIVALSText sibling.
            SetButton(startButton, viewModel.CanStartOrContinue);
            SetButton(garageButton, viewModel.CanOpenGarage);
            SetButton(settingsButton, viewModel.CanOpenSettings);
            SetButton(quitButton, viewModel.CanQuit);
            if (navigationController != null)
            {
                navigationController.RefreshNavigation();
            }

            RenderMenuText(viewModel);
            if (loadingBlocker != null)
            {
                loadingBlocker.SetActive(viewModel.IsTransitioning);
            }

            UguiViewText.Set(errorText, viewModel.Failure == PresentationFailure.None ? StatusText(viewModel.MessageKey) : StatusText(viewModel.MessageKey));
        }

        private void RenderMenuText(MainMenuViewModel viewModel)
        {
            UguiViewText.Set(startButtonPrimaryText, "PLAY");
            UguiViewText.Set(startButtonDescriptionText, "Start Race");
            UguiViewText.Set(garageButtonPrimaryText, "GARAGE");
            UguiViewText.Set(garageButtonDescriptionText, "Vehicle Upgrades");
            UguiViewText.Set(settingsButtonPrimaryText, "SETTINGS");
            UguiViewText.Set(settingsButtonDescriptionText, "Game Settings");
            UguiViewText.Set(quitButtonPrimaryText, "EXIT");
            UguiViewText.Set(quitButtonDescriptionText, "Exit Game");

            if (startButtonPrimaryText == null && startButtonDescriptionText == null)
            {
                UguiViewText.Set(startButton != null ? startButton.GetComponentInChildren<Text>() : null, viewModel.StartTarget == MainMenuStartTarget.Race ? "Start Race" : "Select Vehicle");
            }
        }

        private void OnEnable()
        {
            Subscribe();
            _presenter?.Present();
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

            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }

            if (garageButton != null)
            {
                garageButton.onClick.AddListener(OnGarageClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
            }

            if (garageButton != null)
            {
                garageButton.onClick.RemoveListener(OnGarageClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitClicked);
            }

            _subscribed = false;
        }

        private async void OnStartClicked()
        {
            if (_presenter == null || !_current.CanStartOrContinue)
            {
                return;
            }

            await Run(_presenter.StartOrContinue());
        }

        private async void OnGarageClicked()
        {
            if (_presenter == null || !_current.CanOpenGarage)
            {
                return;
            }

            await Run(_presenter.OpenGarage());
        }

        private void OnSettingsClicked()
        {
            if (_current.CanOpenSettings)
            {
                _presenter?.OpenSettings();
            }
        }

        private void OnQuitClicked()
        {
            if (_current.CanQuit)
            {
                _presenter?.Quit();
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

        private static string StatusText(string messageKey)
        {
            return string.IsNullOrWhiteSpace(messageKey) ? string.Empty : messageKey;
        }
    }
}
