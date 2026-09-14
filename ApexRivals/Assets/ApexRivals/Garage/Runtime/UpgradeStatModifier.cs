using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.Garage.Runtime
{
    public readonly struct UpgradeStatModifier
    {
        public UpgradeStatModifier(
            float accelerationMultiplier,
            float maximumSpeedMultiplier,
            float steeringMultiplier,
            float normalGripBonus,
            float driftGripBonus)
        {
            AccelerationMultiplier = accelerationMultiplier;
            MaximumSpeedMultiplier = maximumSpeedMultiplier;
            SteeringMultiplier = steeringMultiplier;
            NormalGripBonus = normalGripBonus;
            DriftGripBonus = driftGripBonus;
        }

        public float AccelerationMultiplier { get; }
        public float MaximumSpeedMultiplier { get; }
        public float SteeringMultiplier { get; }
        public float NormalGripBonus { get; }
        public float DriftGripBonus { get; }

        public static UpgradeStatModifier Identity => new UpgradeStatModifier(1f, 1f, 1f, 0f, 0f);

        public VehiclePerformanceStats ApplyTo(VehiclePerformanceStats baseStats)
        {
            return new VehiclePerformanceStats(
                baseStats.Acceleration * AccelerationMultiplier,
                baseStats.TopSpeed * MaximumSpeedMultiplier,
                baseStats.Steering * SteeringMultiplier,
                UnityEngine.Mathf.Max(0f, baseStats.Handling + NormalGripBonus),
                UnityEngine.Mathf.Max(0f, baseStats.DriftHandling + DriftGripBonus));
        }
    }
}
