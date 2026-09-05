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
            ArcadeVehicleStats baseStats,
            ArcadeVehicleStats effectivePlayerStats)
        {
            VehicleId = vehicleId;
            DisplayName = displayName;
            Selectable = selectable;
            Selected = selected;
            BaseStats = baseStats;
            EffectivePlayerStats = effectivePlayerStats;
        }

        public string VehicleId { get; }
        public string DisplayName { get; }
        public bool Selectable { get; }
        public bool Selected { get; }
        public ArcadeVehicleStats BaseStats { get; }
        public ArcadeVehicleStats EffectivePlayerStats { get; }
    }
}
