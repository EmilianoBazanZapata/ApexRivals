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
        private string _candidateVehicleId;

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
            _candidateVehicleId = CreateInitialCandidateVehicleId();
        }

        public event Action<VehicleSelectionChangedEvent> SelectionChanged;
        public event Action<VehicleSelectionCommittedEvent> VehicleSelectionCommitted;

        public string CurrentSelectedVehicleId => _selectedVehicleState.HasCommittedVehicleSelection ? _selectedVehicleState.SelectedVehicleId : _candidateVehicleId;
        public string CurrentCandidateVehicleId => _candidateVehicleId;
        public bool HasCommittedVehicleSelection => _selectedVehicleState.HasCommittedVehicleSelection;
        public VehicleDefinitionData CurrentSelectedDefinition => _selectedVehicleState.HasCommittedVehicleSelection
            ? GetCommittedVehicleDefinition()
            : GetCandidateVehicleDefinition();
        public IReadOnlyList<VehicleDefinitionData> AvailableVehicles => _catalog.GetSelectableDemoVehicles();

        public VehicleLookupResult EnsurePersistedSelectionIsValid()
        {
            if (!_selectedVehicleState.HasCommittedVehicleSelection)
            {
                _candidateVehicleId = CreateInitialCandidateVehicleId();
                return new VehicleLookupResult(VehicleCatalogStatus.Succeeded, GetCandidateVehicleDefinition(), string.Empty);
            }

            var lookup = _catalog.FindById(_selectedVehicleState.SelectedVehicleId);
            if (!lookup.Succeeded || !lookup.Vehicle.AvailableInDemo)
            {
                _selectedVehicleState.ClearCommitment();
                _candidateVehicleId = CreateInitialCandidateVehicleId();
                return new VehicleLookupResult(VehicleCatalogStatus.Succeeded, null, "The committed vehicle selection is invalid and must be selected again.");
            }

            _candidateVehicleId = lookup.Vehicle.StableId;
            return new VehicleLookupResult(VehicleCatalogStatus.Succeeded, lookup.Vehicle, string.Empty);
        }

        public VehicleSelectionState GetState()
        {
            var selectedId = CurrentSelectedVehicleId;
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

            return new VehicleSelectionState(selectedId, selectedItem, items, _selectedVehicleState.HasCommittedVehicleSelection);
        }

        public VehicleSelectionResult Select(string vehicleId)
        {
            if (_selectedVehicleState.HasCommittedVehicleSelection)
            {
                var committedId = _selectedVehicleState.SelectedVehicleId;
                var requestedId = string.IsNullOrWhiteSpace(vehicleId) ? string.Empty : vehicleId.Trim();
                return requestedId == committedId
                    ? new VehicleSelectionResult(VehicleSelectionStatus.Succeeded, committedId, committedId, string.Empty)
                    : new VehicleSelectionResult(VehicleSelectionStatus.SelectionLocked, committedId, committedId, "The vehicle selection is already committed for this profile.");
            }

            var lookup = _catalog.FindById(vehicleId);
            if (!lookup.Succeeded)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.UnknownVehicle, CurrentSelectedVehicleId, CurrentSelectedVehicleId, lookup.Message);
            }

            if (!lookup.Vehicle.AvailableInDemo)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.UnavailableVehicle, CurrentSelectedVehicleId, CurrentSelectedVehicleId, "The selected vehicle is not available in the demo.");
            }

            var previousVehicleId = _candidateVehicleId;
            if (previousVehicleId == lookup.Vehicle.StableId)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.Succeeded, previousVehicleId, previousVehicleId, string.Empty);
            }

            _candidateVehicleId = lookup.Vehicle.StableId;
            SelectionChanged?.Invoke(new VehicleSelectionChangedEvent(previousVehicleId, lookup.Vehicle.StableId));
            return new VehicleSelectionResult(VehicleSelectionStatus.Succeeded, previousVehicleId, lookup.Vehicle.StableId, string.Empty);
        }

        public VehicleSelectionResult ConfirmSelection()
        {
            if (string.IsNullOrWhiteSpace(_candidateVehicleId))
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.MissingSelection, CurrentSelectedVehicleId, CurrentSelectedVehicleId, "A vehicle must be selected before confirmation.");
            }

            var lookup = _catalog.FindById(_candidateVehicleId);
            if (!lookup.Succeeded)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.UnknownVehicle, CurrentSelectedVehicleId, CurrentSelectedVehicleId, lookup.Message);
            }

            if (!lookup.Vehicle.AvailableInDemo)
            {
                return new VehicleSelectionResult(VehicleSelectionStatus.UnavailableVehicle, CurrentSelectedVehicleId, CurrentSelectedVehicleId, "The selected vehicle is not available in the demo.");
            }

            if (_selectedVehicleState.HasCommittedVehicleSelection)
            {
                return _selectedVehicleState.SelectedVehicleId == lookup.Vehicle.StableId
                    ? new VehicleSelectionResult(VehicleSelectionStatus.Succeeded, lookup.Vehicle.StableId, lookup.Vehicle.StableId, string.Empty)
                    : new VehicleSelectionResult(VehicleSelectionStatus.SelectionLocked, _selectedVehicleState.SelectedVehicleId, _selectedVehicleState.SelectedVehicleId, "The vehicle selection is already committed for this profile.");
            }

            var previousVehicleId = _selectedVehicleState.SelectedVehicleId;
            _selectedVehicleState.Commit(lookup.Vehicle.StableId);

            if (_saveCheckpoint != null)
            {
                var saveResult = _saveCheckpoint.Save();
                if (!saveResult.Succeeded)
                {
                    _selectedVehicleState.ClearCommitment();
                    return new VehicleSelectionResult(VehicleSelectionStatus.SaveFailed, previousVehicleId, previousVehicleId, saveResult.Message);
                }
            }

            VehicleSelectionCommitted?.Invoke(new VehicleSelectionCommittedEvent(lookup.Vehicle.StableId));
            return new VehicleSelectionResult(VehicleSelectionStatus.Succeeded, previousVehicleId, lookup.Vehicle.StableId, string.Empty);
        }

        public void CancelPreview()
        {
            _candidateVehicleId = _selectedVehicleState.HasCommittedVehicleSelection
                ? _selectedVehicleState.SelectedVehicleId
                : CreateInitialCandidateVehicleId();
        }

        private VehicleDefinitionData GetCommittedVehicleDefinition()
        {
            if (!_selectedVehicleState.HasCommittedVehicleSelection)
            {
                return null;
            }

            var lookup = _catalog.FindById(_selectedVehicleState.SelectedVehicleId);
            return lookup.Succeeded && lookup.Vehicle.AvailableInDemo ? lookup.Vehicle : null;
        }

        private VehicleDefinitionData GetCandidateVehicleDefinition()
        {
            var lookup = _catalog.FindById(_candidateVehicleId);
            return lookup.Succeeded && lookup.Vehicle.AvailableInDemo ? lookup.Vehicle : null;
        }

        private string CreateInitialCandidateVehicleId()
        {
            if (_selectedVehicleState.HasCommittedVehicleSelection)
            {
                return _selectedVehicleState.SelectedVehicleId;
            }

            var defaultLookup = _catalog.FindById(_catalog.DefaultVehicleId);
            if (defaultLookup.Succeeded && defaultLookup.Vehicle.AvailableInDemo)
            {
                return defaultLookup.Vehicle.StableId;
            }

            var vehicles = AvailableVehicles;
            return vehicles.Count > 0 ? vehicles[0].StableId : string.Empty;
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
