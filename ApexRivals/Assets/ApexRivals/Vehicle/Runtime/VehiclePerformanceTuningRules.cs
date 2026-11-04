using ApexRivals.Vehicle.Configuration;
using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    public static class VehiclePerformanceTuningRules
    {
        public static float RelativeMultiplier(float baseline, float effective)
        {
            if (!IsPositiveFinite(baseline) || !IsPositiveFinite(effective)) return 1f;
            var ratio = effective / baseline;
            return IsPositiveFinite(ratio) ? ratio : 1f;
        }

        public static WheelVehicleTuning Apply(WheelVehicleTuning tuning, VehiclePerformanceStats baseline, VehiclePerformanceStats effective)
        {
            tuning.motorPower *= RelativeMultiplier(baseline.Acceleration, effective.Acceleration);
            var normalGrip = RelativeMultiplier(baseline.Handling, effective.Handling);
            var driftGrip = RelativeMultiplier(baseline.DriftHandling, effective.DriftHandling);
            tuning.frontSidewaysFriction.stiffness *= normalGrip;
            tuning.rearSidewaysFriction.stiffness *= normalGrip;
            // Rear drift grip is relative to the original drift setup, not compounded
            // with the normal-grip upgrade a second time.
            tuning.driftRearSidewaysGripMultiplier *= driftGrip / normalGrip;
            return tuning;
        }

        public static float ForwardMotorFactor(float signedForwardSpeed, float maximumSpeed)
        {
            if (!IsPositiveFinite(maximumSpeed)) return 1f;
            // Limit propulsion, not velocity: collisions, gravity and reverse keep
            // their physical behavior. Ease off during the last 5% of the speed range.
            return Mathf.Clamp01((maximumSpeed - signedForwardSpeed) / Mathf.Max(0.01f, maximumSpeed * 0.05f));
        }

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
