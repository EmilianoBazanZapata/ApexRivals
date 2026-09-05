using System;
using System.Collections.Generic;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.VehicleSelection.Runtime
{
    public sealed class VehicleSelectionService
    {
        private readonly VehicleCatalog _catalog;
        private readonly SelectedVehicleState _selectedVehicleState;
        private readonly IVehicleSelectionSaveCheckpoint _saveCheckpoint;
        private readonly PlayerProgressionState _progressionState;
        private readonly UpgradeDefinitionData _engineDefinition;
        private readonly UpgradeDefinitionData _handlingDefinition;

        public VehicleSelectionService(
            VehicleCatalog catalog,
            SelectedVehicleState selectedVehicleState,
            IVehicleSelectionSaveCheckpoint saveCheckpoint = null,
            PlayerProgressionState progressionState = null,
            UpgradeDefinitionData engineDefinition = null,
            UpgradeDefinitionData handlingDefinition = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _selectedVehicleState = selectedVehicleState ?? throw new ArgumentNullException(nameof(selectedVehicleState));
            _saveCheckpoint = saveCheckpoint;
            _progressionState = progressionState;
            _engineDefinition = engineDefinition;
            _handlingDefinition = handlingDefinition;
        }

        public event Action<VehicleSelectionChangedEvent> SelectionChanged;

        public string CurrentSelectedVehicleId => _selectedVehicleState.SelectedVehicleId;
        public VehicleDefinitionData CurrentSelectedDefinition => _catalog.ResolveSavedOrDefault(_selectedVehicleState.SelectedVehicleId);
        public IReadOnlyList<VehicleDefinitionData> AvailableVehicles => _catalog.GetSelectableDemoVehicles();

        public VehicleLookupResult EnsurePersistedSelectionIsValid()
        {
            var resolvedVehicle = _catalog.ResolveSavedOrDefault(_selectedVehicleState.SelectedVehicleId);
            if (resolvedVehicle == null)
            {
                return new VehicleLookupResult(VehicleCatalogStatus.InvalidDefaultVehicleId, null, "The persisted vehicle selection could not be resolved to a valid default.");
            }

            if (_selectedVehicleState.SelectedVehicleId != resolvedVehicle.StableId)
            {
                _selectedVehicleState.SetSelectedVehicleId(resolvedVehicle.StableId);
            }

            return new VehicleLookupResult(VehicleCatalogStatus.Succeeded, resolvedVehicle, string.Empty);
        }

        public VehicleSelectionState GetState()
        {
            var selectedId = CurrentSelectedDefinition?.StableId ?? string.Empty;
            var items = new List<VehicleSelectionItemState>();

            foreach (var vehicle in _catalog.Vehicles)
            {
                if (vehicle == null || !vehicle.HasValidId)
                {
                    continue;
                }

                var baseStats = vehicle.BaseStats;
                var effectiveStats = CreateEffectiveStats(baseStats);
                items.Add(new VehicleSelectionItemState(
                    vehicle.StableId,
                    vehicle.DisplayName,
                    vehicle.AvailableInDemo,
                    vehicle.StableId == selectedId,
                    baseStats,
                    effectiveStats));
            }

            var selectedItem = default(VehicleSelectionItemState);
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index].Selected)
                {
                    selectedItem = items[index];
                    break;
                }
            }

            return new VehicleSelectionState(selectedId, selectedItem, items);
        }

        public VehicleSelectionResult Select(string vehicleId)
        {
            var lookup = _catalog.FindById(vehicleId);
            if (!lookup.Succeeded)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.UnknownVehicle, CurrentSelectedVehicleId, CurrentSelectedVehicleId, lookup.Message);
            }

            if (!lookup.Vehicle.AvailableInDemo)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.UnavailableVehicle, CurrentSelectedVehicleId, CurrentSelectedVehicleId, "The selected vehicle is not available in the demo.");
            }

            var previousVehicleId = CurrentSelectedVehicleId;
            if (previousVehicleId == lookup.Vehicle.StableId)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.Succeeded, previousVehicleId, previousVehicleId, string.Empty);
            }

            _selectedVehicleState.SetSelectedVehicleId(lookup.Vehicle.StableId);

            if (_saveCheckpoint != null)
            {
                var saveResult = _saveCheckpoint.Save();
                if (!saveResult.Succeeded)
                {
                    _selectedVehicleState.SetSelectedVehicleId(previousVehicleId);
                    return new VehicleSelectionResult(VehicleSelectionStatus.SaveFailed, previousVehicleId, previousVehicleId, saveResult.Message);
                }
            }

            SelectionChanged?.Invoke(new VehicleSelectionChangedEvent(previousVehicleId, lookup.Vehicle.StableId));
            return new VehicleSelectionResult(VehicleSelectionStatus.Succeeded, previousVehicleId, lookup.Vehicle.StableId, string.Empty);
        }

        private ArcadeVehicleStats CreateEffectiveStats(ArcadeVehicleStats baseStats)
        {
            if (_progressionState == null || _engineDefinition == null || _handlingDefinition == null)
            {
                return baseStats;
            }

            return VehicleUpgradeStatCalculator.Calculate(baseStats, _progressionState, _engineDefinition, _handlingDefinition);
        }
    }
}
