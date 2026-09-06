using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageUguiView : MonoBehaviour, IGarageView
    {
        [SerializeField] private Text currencyText;
        [SerializeField] private Text vehicleNameText;
        [SerializeField] private Text engineLevelText;
        [SerializeField] private Text enginePriceText;
        [SerializeField] private Button engineUpgradeButton;
        [SerializeField] private Text handlingLevelText;
        [SerializeField] private Text handlingPriceText;
        [SerializeField] private Button handlingUpgradeButton;
        [SerializeField] private Text statsText;
        [SerializeField] private Button startRaceButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private Text statusText;

        private GaragePresenter _presenter;
        private bool _subscribed;

        public void Bind(GaragePresenter presenter)
        {
            _presenter = presenter;
            Subscribe();
        }

        public void Render(GarageViewModel viewModel)
        {
            UguiViewText.Set(currencyText, $"Currency: {viewModel.Currency}");
            UguiViewText.Set(vehicleNameText, viewModel.SelectedVehicleName);
            UguiViewText.Set(engineLevelText, $"Engine: {viewModel.Engine.CurrentLevel}/{viewModel.Engine.MaximumLevel}");
            UguiViewText.Set(enginePriceText, viewModel.Engine.IsAtMaximum ? "Max Level" : $"Next: {viewModel.Engine.NextPrice}");
            UguiViewText.Set(handlingLevelText, $"Handling: {viewModel.Handling.CurrentLevel}/{viewModel.Handling.MaximumLevel}");
            UguiViewText.Set(handlingPriceText, viewModel.Handling.IsAtMaximum ? "Max Level" : $"Next: {viewModel.Handling.NextPrice}");
            UguiViewText.Set(statsText, UguiViewText.FormatStats(viewModel.EffectiveStats));
            UguiViewText.Set(statusText, viewModel.MessageKey);
            SetButton(engineUpgradeButton, viewModel.Engine.CanPurchase && !viewModel.OperationActive);
            SetButton(handlingUpgradeButton, viewModel.Handling.CanPurchase && !viewModel.OperationActive);
            SetButton(startRaceButton, !viewModel.OperationActive);
            SetButton(returnToMainMenuButton, !viewModel.OperationActive);
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

            if (engineUpgradeButton != null)
            {
                engineUpgradeButton.onClick.AddListener(OnEngineUpgradeClicked);
            }

            if (handlingUpgradeButton != null)
            {
                handlingUpgradeButton.onClick.AddListener(OnHandlingUpgradeClicked);
            }

            if (startRaceButton != null)
            {
                startRaceButton.onClick.AddListener(OnStartRaceClicked);
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

            if (engineUpgradeButton != null)
            {
                engineUpgradeButton.onClick.RemoveListener(OnEngineUpgradeClicked);
            }

            if (handlingUpgradeButton != null)
            {
                handlingUpgradeButton.onClick.RemoveListener(OnHandlingUpgradeClicked);
            }

            if (startRaceButton != null)
            {
                startRaceButton.onClick.RemoveListener(OnStartRaceClicked);
            }

            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);
            }

            _subscribed = false;
        }

        private void OnEngineUpgradeClicked()
        {
            _presenter?.PurchaseEngine();
        }

        private void OnHandlingUpgradeClicked()
        {
            _presenter?.PurchaseHandling();
        }

        private async void OnStartRaceClicked()
        {
            if (_presenter != null)
            {
                await Run(_presenter.StartRace());
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
