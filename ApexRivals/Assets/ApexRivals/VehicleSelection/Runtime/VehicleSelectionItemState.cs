using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleSelectionItemState
    {
        public VehicleSelectionItemState(
            string vehicleId,
            string displayName,
            bool selectable,
            bool selected,
            VehiclePerformanceStats baseStats,
            VehiclePerformanceStats effectivePlayerStats,
            bool owned = true)
        {
            VehicleId = vehicleId;
            DisplayName = displayName;
            Selectable = selectable;
            Selected = selected;
            BaseStats = baseStats;
            EffectivePlayerStats = effectivePlayerStats;
            Owned = owned;
        }

        /// <summary>False for cars that can be browsed but must be bought before racing.</summary>
        public bool Owned { get; }

        public string VehicleId { get; }
        public string DisplayName { get; }
        public bool Selectable { get; }
        public bool Selected { get; }
        public VehiclePerformanceStats BaseStats { get; }
        public VehiclePerformanceStats EffectivePlayerStats { get; }
    }
}
