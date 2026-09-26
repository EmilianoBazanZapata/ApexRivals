using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.UI.Runtime
{
    public readonly struct VehicleSelectionItemViewModel
    {
        public VehicleSelectionItemViewModel(
            string vehicleId,
            string displayName,
            VehiclePerformanceStats baseStats,
            VehiclePerformanceStats effectiveStats,
            bool isSelected,
            bool canSelect)
        {
            VehicleId = vehicleId;
            DisplayName = displayName;
            BaseStats = baseStats;
            EffectiveStats = effectiveStats;
            IsSelected = isSelected;
            CanSelect = canSelect;
        }

        public string VehicleId { get; }
        public string DisplayName { get; }
        public VehiclePerformanceStats BaseStats { get; }
        public VehiclePerformanceStats EffectiveStats { get; }
        public bool IsSelected { get; }
        public bool CanSelect { get; }
    }
}
