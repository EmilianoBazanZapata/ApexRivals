using System;
using System.Threading.Tasks;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class GaragePresenter
    {
        private readonly IGarageView _view;
        private readonly GarageService _garageService;
        private readonly VehicleSelectionService _vehicleSelectionService;
        private readonly SceneFlowService _sceneFlow;
        private bool _operationActive;
        private PresentationStatus _purchaseStatus;
        private string _messageKey = string.Empty;

        public GaragePresenter(IGarageView view, GarageService garageService, VehicleSelectionService vehicleSelectionService, SceneFlowService sceneFlow)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _garageService = garageService ?? throw new ArgumentNullException(nameof(garageService));
            _vehicleSelectionService = vehicleSelectionService ?? throw new ArgumentNullException(nameof(vehicleSelectionService));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public GarageViewModel Current { get; private set; }

        public void Present()
        {
            Current = CreateModel();
            _view.Render(Current);
        }

        public void PurchaseEngine()
        {
            Purchase(UpgradeType.Engine);
        }

        public void PurchaseHandling()
        {
            Purchase(UpgradeType.Handling);
        }

        public async Task StartRace()
        {
            await RunNavigation(_sceneFlow.StartRace, "ui.garage.startRaceFailed");
        }

        public async Task ReturnToMainMenu()
        {
            await RunNavigation(_sceneFlow.OpenMainMenu, "ui.garage.returnMainMenuFailed");
        }

        private void Purchase(UpgradeType upgradeType)
        {
            if (_operationActive)
            {
                _purchaseStatus = PresentationStatus.Invalid;
                _messageKey = "ui.garage.operationActive";
                Present();
                return;
            }

            _operationActive = true;
            Present();
            var result = _garageService.Purchase(upgradeType);
            _operationActive = false;
            _purchaseStatus = MapPurchaseStatus(result.Status);
            _messageKey = CreatePurchaseMessageKey(result.Status);
            Present();
        }

        private async Task RunNavigation(Func<Task<SceneTransitionResult>> transition, string failureKey)
        {
            if (_operationActive)
            {
                return;
            }

            _operationActive = true;
            Present();
            var result = await transition();
            _operationActive = false;
            if (!result.Succeeded)
            {
                _purchaseStatus = PresentationStatus.Invalid;
                _messageKey = failureKey;
            }

            Present();
        }

        private GarageViewModel CreateModel()
        {
            var state = _garageService.GetState();
            var selectedVehicleName = _vehicleSelectionService.CurrentSelectedDefinition?.DisplayName ?? string.Empty;
            return new GarageViewModel(
                state.Currency,
                selectedVehicleName,
                MapUpgrade(state.Engine),
                MapUpgrade(state.Handling),
                state.EffectiveStats,
                _operationActive,
                _purchaseStatus,
                _messageKey);
        }

        private static GarageUpgradeViewModel MapUpgrade(GarageUpgradeState upgrade)
        {
            return new GarageUpgradeViewModel(upgrade.UpgradeType, upgrade.CurrentLevel, upgrade.MaximumLevel, upgrade.NextPrice, upgrade.CanPurchase);
        }

        private static PresentationStatus MapPurchaseStatus(UpgradePurchaseStatus status)
        {
            return status switch
            {
                UpgradePurchaseStatus.Succeeded => PresentationStatus.Succeeded,
                UpgradePurchaseStatus.InsufficientCurrency => PresentationStatus.InsufficientCurrency,
                UpgradePurchaseStatus.MaximumLevelReached => PresentationStatus.MaximumLevelReached,
                _ => PresentationStatus.Invalid
            };
        }

        private static string CreatePurchaseMessageKey(UpgradePurchaseStatus status)
        {
            return status switch
            {
                UpgradePurchaseStatus.Succeeded => "ui.garage.purchaseSucceeded",
                UpgradePurchaseStatus.InsufficientCurrency => "ui.garage.insufficientCurrency",
                UpgradePurchaseStatus.MaximumLevelReached => "ui.garage.maximumLevel",
                _ => "ui.garage.purchaseFailed"
            };
        }
    }
}
