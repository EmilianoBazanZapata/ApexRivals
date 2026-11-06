using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.Garage.Runtime
{
    public static class VehicleUpgradeStatCalculator
    {
        public static VehiclePerformanceStats Calculate(
            VehiclePerformanceStats baseStats,
            PlayerProgressionState progressionState,
            UpgradeDefinitionData engineDefinition,
            UpgradeDefinitionData handlingDefinition)
        {
            var engineLevel = progressionState.GetUpgradeLevel(UpgradeType.Engine);
            var handlingLevel = progressionState.GetUpgradeLevel(UpgradeType.Handling);
            var engineStats = engineDefinition.GetModifierForLevel(engineLevel).ApplyTo(baseStats);
            return handlingDefinition.GetModifierForLevel(handlingLevel).ApplyTo(engineStats);
        }
    }
}
