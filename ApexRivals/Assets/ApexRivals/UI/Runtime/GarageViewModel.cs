using System.Collections.Generic;
using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.UI.Runtime
{
    public readonly struct GarageViewModel
    {
        public GarageViewModel(
            int currency,
            string selectedVehicleName,
            GarageUpgradeViewModel engine,
            GarageUpgradeViewModel handling,
            VehiclePerformanceStats effectiveStats,
            bool operationActive,
            PresentationStatus purchaseStatus,
            string messageKey)
            : this(currency, selectedVehicleName, new List<VehicleSelectionItemViewModel>(), -1, !string.IsNullOrWhiteSpace(selectedVehicleName), engine, handling, effectiveStats, operationActive, purchaseStatus, messageKey)
        {
        }

        public GarageViewModel(
            int currency,
            string selectedVehicleName,
            IReadOnlyList<VehicleSelectionItemViewModel> vehicles,
            int candidateVehicleIndex,
            bool hasValidSelectedVehicle,
            GarageUpgradeViewModel engine,
            GarageUpgradeViewModel handling,
            VehiclePerformanceStats effectiveStats,
            bool operationActive,
            PresentationStatus purchaseStatus,
            string messageKey)
        {
            Currency = currency;
            SelectedVehicleName = selectedVehicleName;
            Vehicles = vehicles;
            CandidateVehicleIndex = candidateVehicleIndex;
            HasValidSelectedVehicle = hasValidSelectedVehicle;
            Engine = engine;
            Handling = handling;
            EffectiveStats = effectiveStats;
            OperationActive = operationActive;
            PurchaseStatus = purchaseStatus;
            MessageKey = messageKey;
        }

        public int Currency { get; }
        public string SelectedVehicleName { get; }
        public IReadOnlyList<VehicleSelectionItemViewModel> Vehicles { get; }
        public int CandidateVehicleIndex { get; }
        public bool HasValidSelectedVehicle { get; }
        public GarageUpgradeViewModel Engine { get; }
        public GarageUpgradeViewModel Handling { get; }
        public VehiclePerformanceStats EffectiveStats { get; }
        public bool OperationActive { get; }
        public PresentationStatus PurchaseStatus { get; }
        public string MessageKey { get; }
    }
}
