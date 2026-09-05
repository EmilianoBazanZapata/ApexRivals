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

        public ArcadeVehicleStats ApplyTo(ArcadeVehicleStats baseStats)
        {
            return new ArcadeVehicleStats(
                baseStats.ForwardAcceleration * AccelerationMultiplier,
                baseStats.ReverseAcceleration,
                baseStats.BrakeForce,
                baseStats.MaximumForwardSpeed * MaximumSpeedMultiplier,
                baseStats.MaximumReverseSpeed,
                baseStats.SteeringStrength * SteeringMultiplier,
                baseStats.MinimumSteeringResponse,
                baseStats.DriftSteeringMultiplier,
                UnityEngine.Mathf.Clamp01(baseStats.NormalLateralGrip + NormalGripBonus),
                UnityEngine.Mathf.Clamp01(baseStats.DriftLateralGrip + DriftGripBonus),
                baseStats.Downforce,
                baseStats.GroundCheckDistance,
                baseStats.GroundLayerMask,
                baseStats.ResetHeightOffset,
                baseStats.ReverseEntrySpeed);
        }
    }
}
