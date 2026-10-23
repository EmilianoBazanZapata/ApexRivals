using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Arcade drift stabiliser. The rear-driven cars resist breaking away and then snap past any useful
    /// angle; this yaw correction damps the rotation that grows the slide as it nears the maximum angle and
    /// pulls it back beyond it. It never yaws the car toward a bigger slide, so drifting still has to be
    /// started and held by the driver.
    /// </summary>
    public static class VehicleDriftAssistRules
    {
        /// <summary>Below this speed (m/s) a slide is a slow pivot, not a drift, and is left alone.</summary>
        public const float MinimumSpeed = 5f;

        /// <summary>Fraction of the maximum slide angle where rotation damping starts.</summary>
        public const float DampingOnsetFraction = 0.6f;

        /// <summary>
        /// Yaw acceleration (rad/s², positive turns right) that keeps the slide in check.
        /// <paramref name="slipAngle"/> is the signed angle from the travel direction to the nose in degrees
        /// (positive: nose right of travel); <paramref name="yawRate"/> is the body yaw rate in rad/s.
        /// </summary>
        public static float CalculateYawAcceleration(float slipAngle, float yawRate, float maximumSlipAngle, float slipGain, float yawDamping, float maximumAcceleration)
        {
            var slip = Mathf.Abs(slipAngle);
            var maximum = Mathf.Max(1f, maximumSlipAngle);
            var onset = maximum * DampingOnsetFraction;
            if (slip <= onset)
            {
                return 0f;
            }

            var side = Mathf.Sign(slipAngle);
            // Rotation in the slide's direction grows the angle; rotation the other way is the car recovering.
            var growing = Mathf.Max(0f, yawRate * side);
            var damping = Mathf.Max(0f, yawDamping) * growing * Mathf.InverseLerp(onset, maximum, slip);
            var restoring = Mathf.Max(0f, slipGain) * Mathf.Max(0f, slip - maximum);
            var correction = Mathf.Min(Mathf.Max(0f, maximumAcceleration), damping + restoring);
            return -side * correction;
        }
    }
}
