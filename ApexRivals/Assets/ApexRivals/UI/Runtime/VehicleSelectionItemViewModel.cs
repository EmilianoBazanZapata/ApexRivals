using ApexRivals.Vehicle.Runtime;
using UnityEngine;

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
            bool canSelect,
            Sprite preview = null)
        {
            VehicleId = vehicleId;
            DisplayName = displayName;
            BaseStats = baseStats;
            EffectiveStats = effectiveStats;
            IsSelected = isSelected;
            CanSelect = canSelect;
            Preview = preview;
        }

        public string VehicleId { get; }
        public string DisplayName { get; }
        public VehiclePerformanceStats BaseStats { get; }
        public VehiclePerformanceStats EffectiveStats { get; }
        public bool IsSelected { get; }
        public bool CanSelect { get; }

        /// <summary>Optional thumbnail (the vehicle definition's preview sprite); screens fall back to the name.</summary>
        public Sprite Preview { get; }
    }
}
