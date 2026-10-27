using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// Pure drift decisions: which corners a driver drifts, how hard it keeps the slide going and when it
    /// straightens up. The vehicle supplies the handbrake kick and the countersteer; the driver only picks
    /// the moment, the throttle and the exit.
    /// </summary>
    public static class AiDriftRules
    {
        /// <summary>Heading change (degrees) a corner needs before a driver with full drift affinity drifts it.</summary>
        public const float MostEagerCornerAngle = 50f;

        /// <summary>Heading change (degrees) a driver with the lowest non-zero affinity still drifts.</summary>
        public const float LeastEagerCornerAngle = 120f;

        /// <summary>Sliding this far toward the outside of the corner is a snap, not a drift: straighten up.</summary>
        public const float WrongWaySlipAngle = 15f;

        /// <summary>Smallest corner a driver drifts; infinity when it never drifts.</summary>
        public static float CalculateMinimumCornerAngle(float affinity)
        {
            return affinity <= 0f
                ? float.PositiveInfinity
                : Mathf.Lerp(LeastEagerCornerAngle, MostEagerCornerAngle, Mathf.Clamp01(affinity));
        }

        /// <summary>Slowest corner speed (m/s) worth drifting; tighter hairpins are braked for, not drifted.</summary>
        public const float MinimumCornerSpeed = 11f;

        public static bool IsDriftCorner(float cornerAngle, float minimumCornerAngle)
        {
            return Mathf.Abs(cornerAngle) >= minimumCornerAngle;
        }

        /// <summary>A corner is drifted when it turns enough and is not a crawl-speed hairpin.</summary>
        public static bool IsDriftCorner(float cornerAngle, float minimumCornerAngle, float cornerSpeed)
        {
            return IsDriftCorner(cornerAngle, minimumCornerAngle) && cornerSpeed >= MinimumCornerSpeed;
        }

        /// <summary>
        /// Slide angle measured toward the inside of the corner: positive when the nose points further into
        /// the turn than the car travels, which is the slide a drift wants; negative is sliding the wrong way.
        /// </summary>
        public static float CalculateSlipIntoCorner(float slipAngle, float cornerAngle)
        {
            // Turning right, the nose rotates right of the travel direction, so the car slides left (negative).
            return cornerAngle >= 0f ? -slipAngle : slipAngle;
        }

        /// <summary>Slip proportional gain: steering per degree of slide away from the target.</summary>
        public const float SlipSteeringGain = 0.025f;

        /// <summary>Yaw damping gain: steering per degree per second of rotation into the corner.</summary>
        public const float YawSteeringGain = 0.012f;

        /// <summary>
        /// The drift button is a handbrake kick: held, the rear wheels lock and the car slows instead of
        /// sliding. The kick ends once the tail is on its way (part of the target slide reached, or the car
        /// already rotating quickly) or after a short pulse, so the rear never stays locked.
        /// </summary>
        public static bool ShouldEndKick(float slipIntoCorner, float targetSlipAngle, float yawIntoCorner, float heldTime, float maximumPulse)
        {
            return slipIntoCorner >= targetSlipAngle * 0.6f || yawIntoCorner >= 50f || heldTime >= maximumPulse;
        }

        /// <summary>The slide has died mid-corner (little angle, no rotation): kick again.</summary>
        public static bool ShouldKickAgain(float slipIntoCorner, float targetSlipAngle, float yawIntoCorner, float sustainTime)
        {
            return sustainTime > 0.4f && slipIntoCorner < targetSlipAngle * 0.3f && yawIntoCorner < 15f;
        }

        /// <summary>
        /// Steering added to hold the slide at the target: too much angle, or the car still rotating into
        /// the corner, steers out (countersteer); too little steers in. The yaw term catches the rotation
        /// before the angle overshoots, which is what keeps a drift from becoming a pendulum or a spin.
        /// </summary>
        public static float CalculateSlideSteering(float slipIntoCorner, float targetSlipAngle, float yawIntoCorner, float cornerDirection)
        {
            return CalculateSlideSteering(slipIntoCorner, targetSlipAngle, yawIntoCorner, cornerDirection, 1f);
        }

        /// <summary>Same correction scaled by how sharply the driver reacts (1 is the full, expert correction).</summary>
        public static float CalculateSlideSteering(float slipIntoCorner, float targetSlipAngle, float yawIntoCorner, float cornerDirection, float gainScale)
        {
            var outward = Mathf.Max(0f, gainScale) * (SlipSteeringGain * (slipIntoCorner - targetSlipAngle) + YawSteeringGain * yawIntoCorner);
            return -Mathf.Sign(cornerDirection) * Mathf.Clamp(outward, -1f, 1f);
        }

        /// <summary>Largest change (degrees) to the held slide angle a driver with no drift skill makes from one drift to the next.</summary>
        public const float SlipTargetSpread = 20f;

        /// <summary>How sharply a driver corrects the slide: half as hard with no skill, fully at skill 1.</summary>
        public static float CalculateSkillGainScale(float driftSkill)
        {
            return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(driftSkill));
        }

        /// <summary>Chance that a drift goes wrong (over-rotated or under-kicked); none for a perfect drifter.</summary>
        public static float CalculateMistakeChance(float driftSkill)
        {
            return 1f - Mathf.Clamp01(driftSkill);
        }

        /// <summary>Held slide angle for one drift: the profile's target, varied by up to the spread scaled by missing skill.</summary>
        public static float CalculateDriftSlipTarget(float targetSlipAngle, float driftSkill, float randomUnit)
        {
            return targetSlipAngle + Mathf.Clamp(randomUnit, -1f, 1f) * SlipTargetSpread * (1f - Mathf.Clamp01(driftSkill));
        }

        /// <summary>The car points where it travels and has stopped rotating: the drift is over.</summary>
        public static bool IsStraight(float slipAngle, float yawRate)
        {
            return Mathf.Abs(slipAngle) < 8f && Mathf.Abs(yawRate) < 30f;
        }

        /// <summary>Angle (degrees) between where the car points and where it travels; positive when sliding right.</summary>
        public static float CalculateSlipAngle(float forwardSpeed, float lateralSpeed)
        {
            return Mathf.Atan2(lateralSpeed, Mathf.Max(0.5f, Mathf.Abs(forwardSpeed))) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Throttle that holds the slide near the target angle: the rear wheels are driven, so power swings
        /// the tail out and lifting lets it come back. Full power below the target, easing off toward the
        /// maximum, never below the minimum the vehicle needs to stay in a forward drift.
        /// </summary>
        public static float CalculateDriftThrottle(float slipIntoCorner, float targetSlipAngle, float maximumSlipAngle, float minimumThrottle)
        {
            var slip = Mathf.Max(0f, slipIntoCorner);
            var target = Mathf.Max(1f, targetSlipAngle);
            var maximum = Mathf.Max(target + 1f, maximumSlipAngle);
            var throttle = slip <= target ? 1f : Mathf.Lerp(1f, minimumThrottle, Mathf.InverseLerp(target, maximum, slip));
            return Mathf.Clamp(throttle, Mathf.Clamp01(minimumThrottle), 1f);
        }

        /// <summary>Skilled drivers progressively recover full power as the slide and rotation settle.</summary>
        public static float CalculateExitThrottle(float slipAngle, float yawRate, float driftSkill)
        {
            var alignment = 1f - Mathf.InverseLerp(8f, 25f, Mathf.Abs(slipAngle));
            var stability = 1f - Mathf.InverseLerp(30f, 90f, Mathf.Abs(yawRate));
            return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(driftSkill) * alignment * stability);
        }

        /// <summary>
        /// Straighten up when the corner is done, the slide is about to become a spin, the car has scrubbed
        /// too much speed or the drift has gone on too long.
        /// </summary>
        public static bool ShouldExitDrift(float remainingCornerAngle, float exitCornerAngle, float slipIntoCorner, float maximumSlipAngle, float speed, float exitSpeed, float elapsed, float maximumDuration)
        {
            return Mathf.Abs(remainingCornerAngle) < exitCornerAngle
                || slipIntoCorner > maximumSlipAngle
                || slipIntoCorner < -WrongWaySlipAngle
                || speed < exitSpeed
                || elapsed > maximumDuration;
        }
    }
}
