using UnityEngine;

namespace ApexRivals.VFX.Runtime
{
    public static class RaceVfxRules
    {
        public static bool ShouldSmoke(float speed, float slipAngle, float wheelSlip, bool grounded,
            bool drivingAllowed, float minimumSpeed, float minimumAngle, float minimumWheelSlip)
        {
            return grounded && drivingAllowed && Mathf.Abs(speed) >= minimumSpeed
                && (Mathf.Abs(slipAngle) >= minimumAngle || Mathf.Abs(wheelSlip) >= minimumWheelSlip);
        }

        public static float ImpactStrength(Vector3 relativeVelocity, Vector3 normal, bool scraping)
        {
            // Ground/landing contacts are not metal-on-wall effects.
            if (normal.sqrMagnitude < 0.01f || Mathf.Abs(normal.normalized.y) > 0.65f) return 0f;
            var closing = Mathf.Abs(Vector3.Dot(relativeVelocity, normal.normalized));
            var tangent = Vector3.ProjectOnPlane(relativeVelocity, normal).magnitude;
            return scraping ? Mathf.Max(closing, Mathf.Max(0f, tangent - 7f) * 0.4f) : closing;
        }

        public static bool ShouldCelebrate(bool isPlayer, int position) => isPlayer && position == 1;

        public static float SkidIntensity(float speed, float slipAngle, float sideSlip, float forwardSlip,
            float braking, bool rearHandbrake, RaceVfxDefinition configuration)
        {
            if (Mathf.Abs(speed) < configuration.skidMinimumSpeed) return 0f;
            var drift = 0f;
            if (Mathf.Abs(slipAngle) >= configuration.minimumSlipAngle || Mathf.Abs(sideSlip) >= configuration.minimumWheelSlip)
                drift = Mathf.Lerp(0.25f, 1f, Mathf.Max(
                    Mathf.InverseLerp(configuration.minimumSlipAngle, 35f, Mathf.Abs(slipAngle)),
                    Mathf.InverseLerp(configuration.minimumWheelSlip, 1f, Mathf.Abs(sideSlip))));
            var brake = braking > 0f && braking >= configuration.skidHardBrakeThreshold
                ? Mathf.Lerp(0.3f, 0.9f, Mathf.Max(braking, Mathf.Clamp01(Mathf.Abs(forwardSlip)))) : 0f;
            return Mathf.Clamp01(Mathf.Max(drift, Mathf.Max(brake, rearHandbrake ? 0.65f : 0f)));
        }
    }
}
