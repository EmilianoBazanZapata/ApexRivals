using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    public static class AiDriverRules
    {
        public const int MinimumWaypointCount = 2;
        public const float MinimumReachDistance = 0.1f;
        public const float MinimumDifficultyMultiplier = 0.1f;

        /// <summary>Below this a smoothed pedal that is being released snaps to exactly zero.</summary>
        public const float PedalReleaseThreshold = 0.01f;

        /// <summary>Keep the next braking zone visible even when faster straights exceed the authored preview.</summary>
        public static float CalculateBrakingPreviewDistance(float speed, float deceleration, float minimumPreview)
        {
            var safeSpeed = Mathf.Max(0f, speed);
            var stoppingDistance = safeSpeed * safeSpeed / (2f * Mathf.Max(0.1f, deceleration));
            return Mathf.Max(Mathf.Max(0f, minimumPreview), stoppingDistance + safeSpeed * 0.5f);
        }

        public static int WrapWaypointIndex(int index, int waypointCount)
        {
            if (waypointCount <= 0)
            {
                return 0;
            }

            var wrapped = index % waypointCount;
            return wrapped < 0 ? wrapped + waypointCount : wrapped;
        }

        public static float UpdateStuckTimer(
            bool raceActive,
            bool expectedToMove,
            float currentSpeed,
            float speedThreshold,
            float deltaTime,
            float currentTimer)
        {
            if (!raceActive || !expectedToMove || currentSpeed > speedThreshold)
            {
                return 0f;
            }

            return currentTimer + Mathf.Max(0f, deltaTime);
        }

        /// <summary>
        /// Caps the steering command at low speed. The vehicle turns its front wheels very far when slow,
        /// and a rear-driven car with wheels near perpendicular scrubs instead of moving; half lock is
        /// already a tight turn there.
        /// </summary>
        public static float LimitLowSpeedSteering(float steering, float speed)
        {
            var limit = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(4f, 15f, Mathf.Abs(speed)));
            return Mathf.Clamp(steering, -limit, limit);
        }

        /// <summary>
        /// Shares the tyres between braking and cornering: hard braking while steering hard spins the
        /// car on low grip, so braking is reduced in proportion to the steering demand.
        /// </summary>
        public static float BlendBrakeWithSteering(float brake, float steering)
        {
            return Mathf.Clamp01(brake) * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01((Mathf.Abs(steering) - 0.4f) / 0.6f));
        }

        /// <summary>
        /// Stability aid: while the car slides sideways, heavy braking or power keeps it spinning, so both
        /// are eased in proportion to the slide until the tyres grip again. Returns the input scale (0.3–1).
        /// </summary>
        public static float CalculateSlideInputScale(float lateralSpeed)
        {
            return Mathf.Lerp(1f, 0.3f, Mathf.InverseLerp(2.5f, 6f, Mathf.Abs(lateralSpeed)));
        }

        /// <summary>
        /// Traction control: the rear-driven cars step out under power in slow corners, and the slide angle
        /// shows it before the sideways speed builds up. Power and braking fade as the angle grows, so an
        /// unplanned slide is caught early instead of being driven into a spin. Returns the input scale.
        /// </summary>
        public static float CalculateTractionScale(float slipAngle)
        {
            return Mathf.Lerp(1f, 0.15f, Mathf.InverseLerp(6f, 18f, Mathf.Abs(slipAngle)));
        }

        /// <summary>
        /// Rejoin aim distance: the further off the line, the further ahead the merge point, so the car
        /// returns at a shallow angle instead of crossing the line and overshooting.
        /// </summary>
        public static float CalculateRejoinLookahead(float distanceFromLine, float minimumLookahead)
        {
            return Mathf.Clamp(distanceFromLine * 2f, Mathf.Max(0f, minimumLookahead), 25f);
        }

        /// <summary>
        /// Exponential smoothing of a pedal toward its target. A pedal being released only approaches zero,
        /// and PhysX keeps a locked wheel locked under any brake torque at all, so a car that locked its
        /// wheels braking for a corner would slide on locked wheels until it stopped. Releasing snaps to zero.
        /// </summary>
        public static float SmoothPedal(float current, float target, float smoothing, float deltaTime)
        {
            var value = smoothing <= 0f ? target : Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * deltaTime));
            return target <= 0f && value < PedalReleaseThreshold ? 0f : value;
        }

        /// <summary>
        /// Launch control: from a standstill full throttle spins one rear wheel through the open
        /// differential and the car barely moves, so throttle is capped at low speed and released as the
        /// car picks up speed.
        /// </summary>
        public static float LimitLaunchThrottle(float throttle, float speed)
        {
            var limit = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(1f, 8f, Mathf.Abs(speed)));
            return Mathf.Min(Mathf.Clamp01(throttle), limit);
        }

        public static float ApplyDifficulty(float value, float difficultyMultiplier)
        {
            return Mathf.Max(0f, value * Mathf.Max(MinimumDifficultyMultiplier, difficultyMultiplier));
        }

    }
}
