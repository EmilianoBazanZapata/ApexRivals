using System;

namespace ApexRivals.Vehicle.Runtime
{
    public static class VehicleDriveInputRules
    {
        /// <summary>
        /// Extreme parking lock makes the undriven front tyres scrub across the road instead of roll.
        /// Keep useful steering while pulling away, then progressively restore the configured range.
        /// Deliberate handbrake drifts and unpowered parking retain their existing steering.
        /// </summary>
        public static float LimitLaunchSteeringAngle(float requestedAngle, float speed, float maximumAngle, bool powered, bool drifting)
        {
            var limit = MathF.Max(0f, maximumAngle);
            if (powered && !drifting)
            {
                var launchLimit = MathF.Min(28f, limit);
                var release = MathF.Min(1f, MathF.Max(0f, (MathF.Abs(speed) - 1f) / 7f));
                limit = launchLimit + (limit - launchLimit) * release;
            }

            return MathF.Min(limit, MathF.Max(-limit, requestedAngle));
        }

        public static float ResolveNormalBrakeInput(
            float throttleInput,
            float brakeInput,
            float signedForwardSpeed,
            float reverseEntrySpeed)
        {
            var threshold = reverseEntrySpeed > 0f ? reverseEntrySpeed : 0f;
            if (throttleInput > 0f && signedForwardSpeed < -threshold)
            {
                return throttleInput;
            }

            return brakeInput > 0f && signedForwardSpeed > threshold ? brakeInput : 0f;
        }

        public static float ResolveMotorInput(
            float throttleInput,
            float brakeInput,
            float signedForwardSpeed,
            float reverseEntrySpeed)
        {
            var threshold = reverseEntrySpeed > 0f ? reverseEntrySpeed : 0f;
            if (throttleInput > 0f && signedForwardSpeed >= -threshold)
            {
                return throttleInput;
            }

            if (brakeInput > 0f && signedForwardSpeed <= threshold)
            {
                return -brakeInput;
            }

            return 0f;
        }
    }
}
