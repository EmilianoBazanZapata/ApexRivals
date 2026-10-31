using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityObject = UnityEngine.Object;
using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class GaragePresenter : IDisposable
    {
        private IGarageView _view;
        private readonly GarageService _garageService;
        private readonly VehicleSelectionService _vehicleSelectionService;
        private readonly SceneFlowService _sceneFlow;
        private readonly TrackSelectionPresenter _trackSelection;
        private readonly CareerService _career;
        private string _careerFeedback = string.Empty;
        private bool _operationActive;
        private PresentationStatus _purchaseStatus;
        private string _messageKey = string.Empty;

        /// <param name="trackSelection">When set, Start Race opens the track select screen instead of loading the race.</param>
        /// <param name="career">When set, cars must be owned to race and locked cars show their price and a buy action.</param>
        public GaragePresenter(IGarageView view, GarageService garageService, VehicleSelectionService vehicleSelectionService, SceneFlowService sceneFlow, TrackSelectionPresenter trackSelection = null, CareerService career = null)
        {
            _career = career;
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _garageService = garageService ?? throw new ArgumentNullException(nameof(garageService));
            _vehicleSelectionService = vehicleSelectionService ?? throw new ArgumentNullException(nameof(vehicleSelectionService));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            _trackSelection = trackSelection;
        }

        public GarageViewModel Current { get; private set; }

        private bool _disposed;
        private bool CanRender => !_disposed && _view != null
            && (_view is not UnityObject unityView || unityView != null);

        public void Dispose()
        {
            _disposed = true;
            _view = null;
        }

        public void Present()
        {
            if (!CanRender) return;

            if (_career != null)
            {
                // Objectives completed by an upgrade purchase are announced here, once.
                var announcements = CareerViewText.FormatAwards(_career.ConsumeAnnouncements());
                if (!string.IsNullOrEmpty(announcements))
                {
                    _careerFeedback = announcements;
                }
            }

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
            if (!CanRender || _operationActive)
            {
                return;
            }

            var result = _vehicleSelectionService.Select(vehicleId);
            _purchaseStatus = result.Succeeded ? PresentationStatus.None : PresentationStatus.Invalid;
            _messageKey = result.Succeeded ? string.Empty : "ui.vehicleSelection.invalidSelection";
            Present();
        }

        /// <summary>
        /// Drops a focused-but-not-equipped vehicle so the preview, stats and Start Race all match the
        /// equipped one again. No-op when nothing is being previewed.
        /// </summary>
        public void CancelVehiclePreview()
        {
            if (!CanRender || _operationActive
                || !_vehicleSelectionService.HasCommittedVehicleSelection
                || _vehicleSelectionService.CurrentCandidateVehicleId == _vehicleSelectionService.CurrentSelectedVehicleId)
            {
                return;
            }

            _vehicleSelectionService.CancelPreview();
            Present();
        }

        /// <summary>Previews and equips <paramref name="vehicleId"/> in one step (click / Submit on a vehicle).</summary>
        public void EquipVehicle(string vehicleId)
        {
            if (!CanRender || _operationActive)
            {
                return;
            }

            var selection = _vehicleSelectionService.Select(vehicleId);
            if (!selection.Succeeded)
            {
                _purchaseStatus = PresentationStatus.Invalid;
                _messageKey = "ui.vehicleSelection.invalidSelection";
                Present();
                return;
            }

            if (!_vehicleSelectionService.IsOwned(selection.SelectedVehicleId))
            {
                // Locked cars are previewed with their price; the equipped car stays the one you race.
                _purchaseStatus = PresentationStatus.None;
                _messageKey = string.Empty;
                _careerFeedback = string.Empty;
                Present();
                return;
            }

            ConfirmVehicle();
        }

        /// <summary>Buys the previewed car through the career and equips it. The career validates everything.</summary>
        public void BuyCandidateVehicle()
        {
            if (!CanRender || _operationActive || _career == null)
            {
                return;
            }

            var vehicleId = _vehicleSelectionService.CurrentCandidateVehicleId;
            _operationActive = true;
            Present();
            var result = _career.PurchaseVehicle(vehicleId);
            _operationActive = false;
            var feedback = CareerViewText.FormatPurchase(result, _career.GetVehicleName(vehicleId));
            var awards = CareerViewText.FormatAwards(result.Awards);
            _purchaseStatus = result.Succeeded ? PresentationStatus.Succeeded : PresentationStatus.Invalid;
            _messageKey = result.Succeeded ? "ui.garage.vehiclePurchased" : "ui.garage.vehiclePurchaseFailed";
            if (result.Succeeded)
            {
                // The purchase is already saved by the career; equipping saves the selection as usual.
                _career.ConsumeAnnouncements();
                ConfirmVehicle();
            }

            _careerFeedback = string.IsNullOrEmpty(awards) ? feedback : feedback + "\n" + awards;
            Present();
        }

        /// <summary>Equips the previous owned vehicle (or previews it when it still has to be bought), wrapping around.</summary>
        public void EquipPreviousVehicle()
        {
            EquipVehicleByOffset(-1);
        }

        /// <summary>Equips the next owned vehicle (or previews it when it still has to be bought), wrapping around.</summary>
        public void EquipNextVehicle()
        {
            EquipVehicleByOffset(1);
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
            if (!CanRender || _operationActive)
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
            if (!CanRender || _operationActive) return;
            if (!_vehicleSelectionService.IsOwned(_vehicleSelectionService.CurrentCandidateVehicleId))
            {
                _vehicleSelectionService.CancelPreview();
            }
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

            if (_trackSelection != null)
            {
                _trackSelection.Open();
                return;
            }

            await RunNavigation(_sceneFlow.StartRace, "ui.garage.startRaceFailed");
        }

        public async Task ReturnToMainMenu()
        {
            await RunNavigation(_sceneFlow.OpenMainMenu, "ui.garage.returnMainMenuFailed");
        }

        private void Purchase(UpgradeType upgradeType)
        {
            if (!CanRender) return;
            if (_operationActive)
            {
                _purchaseStatus = PresentationStatus.Invalid;
                _messageKey = "ui.garage.operationActive";
                Present();
                return;
            }

            _operationActive = true;
            _careerFeedback = string.Empty;
            Present();
            var result = _garageService.Purchase(upgradeType);
            _operationActive = false;
            _purchaseStatus = MapPurchaseStatus(result.Status);
            _messageKey = CreatePurchaseMessageKey(result.Status);
            Present();
        }

        private async Task RunNavigation(Func<Task<SceneTransitionResult>> transition, string failureKey)
        {
            if (!CanRender || _operationActive)
            {
                return;
            }

            _operationActive = true;
            Present();
            var result = await transition();
            if (!CanRender) return;
            _operationActive = false;
            if (!result.Succeeded)
            {
                _purchaseStatus = PresentationStatus.Invalid;
                _messageKey = failureKey;
            }

            Present();
        }

        private static UnityEngine.Sprite FindPreview(IReadOnlyList<VehicleDefinitionData> definitions, string vehicleId)
        {
            if (definitions == null)
            {
                return null;
            }

            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null && definitions[index].StableId == vehicleId)
                {
                    return definitions[index].PreviewSprite;
                }
            }

            return null;
        }

        private GarageViewModel CreateModel()
        {
            var state = _garageService.GetState();
            var selectionState = _vehicleSelectionService.GetState();
            var vehicles = new List<VehicleSelectionItemViewModel>(selectionState.Vehicles.Count);
            var equippedIndex = -1;
            var candidateIndex = -1;
            var candidateId = _vehicleSelectionService.CurrentCandidateVehicleId;
            var definitions = _vehicleSelectionService.AvailableVehicles;
            for (var index = 0; index < selectionState.Vehicles.Count; index++)
            {
                var vehicle = selectionState.Vehicles[index];
                // Selected = equipped (committed) vehicle; the candidate is what the player is previewing.
                vehicles.Add(new VehicleSelectionItemViewModel(
                    vehicle.VehicleId,
                    vehicle.DisplayName,
                    vehicle.BaseStats,
                    vehicle.EffectivePlayerStats,
                    vehicle.Selected,
                    vehicle.Selectable,
                    FindPreview(definitions, vehicle.VehicleId)));

                if (vehicle.Selected)
                {
                    equippedIndex = vehicles.Count - 1;
                }

                if (vehicle.VehicleId == candidateId)
                {
                    candidateIndex = vehicles.Count - 1;
                }
            }

            if (candidateIndex < 0)
            {
                candidateIndex = equippedIndex;
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
                _messageKey,
                CreateCareerModel(candidateId, state.Currency));
        }

        private GarageCareerViewModel CreateCareerModel(string candidateId, int currency)
        {
            if (_career == null)
            {
                return default;
            }

            var status = _career.GetVehicleStatus(candidateId);
            return new GarageCareerViewModel(
                CareerViewText.CreatePanel(_career.GetSummary()),
                _career.GetVehicleName(candidateId),
                status.Owned,
                CareerViewText.FormatVehicleStatus(status, currency),
                status.CanPurchase && !_operationActive,
                _careerFeedback);
        }

        private void SelectVehicleByOffset(int offset)
        {
            if (!CanRender || _operationActive || Current.Vehicles == null || Current.Vehicles.Count == 0)
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

        private void EquipVehicleByOffset(int offset)
        {
            if (!CanRender || _operationActive || Current.Vehicles == null || Current.Vehicles.Count == 0)
            {
                return;
            }

            // Step from the car on screen (a locked car being previewed, or the equipped one).
            var vehicles = Current.Vehicles;
            var startIndex = Current.CandidateVehicleIndex;
            if (startIndex < 0)
            {
                startIndex = offset > 0 ? -1 : 0;
            }

            for (var step = 1; step <= vehicles.Count; step++)
            {
                var index = ((startIndex + offset * step) % vehicles.Count + vehicles.Count) % vehicles.Count;
                if (index != Current.CandidateVehicleIndex && vehicles[index].CanSelect)
                {
                    EquipVehicle(vehicles[index].VehicleId);
                    return;
                }
            }
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
