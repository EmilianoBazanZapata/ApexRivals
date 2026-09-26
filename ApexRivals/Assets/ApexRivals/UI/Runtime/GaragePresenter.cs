using System;
using System.Collections.Generic;
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

        public void SelectVehicle(string vehicleId)
        {
            if (_operationActive)
            {
                return;
            }

            var result = _vehicleSelectionService.Select(vehicleId);
            _purchaseStatus = result.Succeeded ? PresentationStatus.None : PresentationStatus.Invalid;
            _messageKey = result.Succeeded ? string.Empty : "ui.vehicleSelection.invalidSelection";
            Present();
        }

        public void SelectPreviousVehicle()
        {
            SelectVehicleByOffset(-1);
        }

        public void SelectNextVehicle()
        {
            SelectVehicleByOffset(1);
        }

        public void ConfirmVehicle()
        {
            if (_operationActive)
            {
                return;
            }

            var result = _vehicleSelectionService.ConfirmSelection();
            _purchaseStatus = result.Succeeded ? PresentationStatus.Succeeded : PresentationStatus.Invalid;
            _messageKey = result.Succeeded
                ? "ui.vehicleSelection.confirmed"
                : result.Status == VehicleSelectionStatus.SaveFailed
                    ? "ui.vehicleSelection.saveFailed"
                    : "ui.vehicleSelection.invalidSelection";
            Present();
        }

        public async Task StartRace()
        {
            if (!_vehicleSelectionService.HasValidCommittedVehicleSelection)
            {
                var selection = _vehicleSelectionService.ConfirmSelection();
                if (!selection.Succeeded || !_vehicleSelectionService.HasValidCommittedVehicleSelection)
                {
                    _purchaseStatus = PresentationStatus.Invalid;
                    _messageKey = selection.Status == VehicleSelectionStatus.SaveFailed
                        ? "ui.vehicleSelection.saveFailed"
                        : "ui.garage.vehicleSelectionRequired";
                    Present();
                    return;
                }
            }

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
            var selectionState = _vehicleSelectionService.GetState();
            var vehicles = new List<VehicleSelectionItemViewModel>(selectionState.Vehicles.Count);
            var candidateIndex = -1;
            for (var index = 0; index < selectionState.Vehicles.Count; index++)
            {
                var vehicle = selectionState.Vehicles[index];
                vehicles.Add(new VehicleSelectionItemViewModel(
                    vehicle.VehicleId,
                    vehicle.DisplayName,
                    vehicle.BaseStats,
                    vehicle.EffectivePlayerStats,
                    vehicle.Selected,
                    vehicle.Selectable));

                if (vehicle.Selected)
                {
                    candidateIndex = vehicles.Count - 1;
                }
            }

            var hasValidVehicle = _vehicleSelectionService.HasValidCommittedVehicleSelection;
            var selectedVehicleName = _vehicleSelectionService.HasValidCommittedVehicleSelection
                ? _vehicleSelectionService.CurrentSelectedDefinition?.DisplayName ?? string.Empty
                : string.Empty;
            return new GarageViewModel(
                state.Currency,
                selectedVehicleName,
                vehicles,
                candidateIndex,
                hasValidVehicle,
                MapUpgrade(state.Engine),
                MapUpgrade(state.Handling),
                state.EffectiveStats,
                _operationActive,
                _purchaseStatus,
                _messageKey);
        }

        private void SelectVehicleByOffset(int offset)
        {
            if (_operationActive || Current.Vehicles == null || Current.Vehicles.Count == 0)
            {
                return;
            }

            var nextIndex = Current.CandidateVehicleIndex < 0 ? 0 : Current.CandidateVehicleIndex + offset;
            if (nextIndex < 0)
            {
                nextIndex = Current.Vehicles.Count - 1;
            }
            else if (nextIndex >= Current.Vehicles.Count)
            {
                nextIndex = 0;
            }

            SelectVehicle(Current.Vehicles[nextIndex].VehicleId);
        }

        private static GarageUpgradeViewModel MapUpgrade(GarageUpgradeState upgrade)
        {
            return new GarageUpgradeViewModel(upgrade.UpgradeType, upgrade.CurrentLevel, upgrade.MaximumLevel, upgrade.NextPrice, upgrade.CanPurchase, upgrade.BenefitModifier);
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
