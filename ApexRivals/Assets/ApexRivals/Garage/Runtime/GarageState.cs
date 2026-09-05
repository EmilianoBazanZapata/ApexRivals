using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.Garage.Runtime
{
    public readonly struct GarageState
    {
        public GarageState(
            int currency,
            GarageUpgradeState engine,
            GarageUpgradeState handling,
            ArcadeVehicleStats effectiveStats)
        {
            Currency = currency;
            Engine = engine;
            Handling = handling;
            EffectiveStats = effectiveStats;
        }

        public int Currency { get; }
        public GarageUpgradeState Engine { get; }
        public GarageUpgradeState Handling { get; }
        public ArcadeVehicleStats EffectiveStats { get; }
    }
}
