using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    public static class AiDriverRules
    {
        public const int MinimumWaypointCount = 2;
        public const float MinimumReachDistance = 0.1f;
        public const float MinimumDifficultyMultiplier = 0.1f;

        public static int WrapWaypointIndex(int index, int waypointCount)
        {
            if (waypointCount <= 0)
            {
                return 0;
            }

            var wrapped = index % waypointCount;
            return wrapped < 0 ? wrapped + waypointCount : wrapped;
        }

        public static float CalculateSteering(Vector3 localTarget, float steeringSensitivity)
        {
            var forwardDistance = Mathf.Max(Mathf.Abs(localTarget.z), 0.001f);
            var angle = Mathf.Atan2(localTarget.x, forwardDistance) * Mathf.Rad2Deg;
            return Mathf.Clamp(angle / 45f * steeringSensitivity, -1f, 1f);
        }

        public static float CalculateCornerSeverity(Vector3 currentDirection, Vector3 nextDirection)
        {
            var currentPlanar = Vector3.ProjectOnPlane(currentDirection, Vector3.up);
            var nextPlanar = Vector3.ProjectOnPlane(nextDirection, Vector3.up);

            if (currentPlanar.sqrMagnitude < 0.0001f || nextPlanar.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            var alignment = Vector3.Dot(currentPlanar.normalized, nextPlanar.normalized);
            return Mathf.Clamp01((1f - alignment) * 0.5f);
        }

        public static float CalculateCornerTargetSpeed(AiDriverTuning tuning, float cornerSeverity)
        {
            return Mathf.Lerp(tuning.TargetSpeed, tuning.MinimumCornerSpeed, Mathf.Clamp01(cornerSeverity));
        }

        public static AiDrivingDecision CreateDecision(float currentSpeed, float steering, float cornerSeverity, AiDriverTuning tuning)
        {
            var targetSpeed = CalculateCornerTargetSpeed(tuning, cornerSeverity);
            var overspeed = currentSpeed - targetSpeed;
            var brake = overspeed > 0f ? Mathf.Clamp01(overspeed / Mathf.Max(targetSpeed, 0.001f) * tuning.BrakingSensitivity) : 0f;
            var throttle = brake > 0.01f ? 0f : Mathf.Clamp01((targetSpeed - currentSpeed) / Mathf.Max(targetSpeed, 0.001f) + 0.35f);
            var drift = cornerSeverity >= tuning.DriftCornerThreshold && currentSpeed >= tuning.DriftMinimumSpeed;

            return new AiDrivingDecision(throttle, brake, Mathf.Clamp(steering, -1f, 1f), drift);
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

        public static bool ShouldRequestRecovery(float stuckTimer, float stuckDuration, float timeSinceLastRecovery, float recoveryCooldown)
        {
            return stuckTimer >= stuckDuration && timeSinceLastRecovery >= recoveryCooldown;
        }

        public static float ApplyDifficulty(float value, float difficultyMultiplier)
        {
            return Mathf.Max(0f, value * Mathf.Max(MinimumDifficultyMultiplier, difficultyMultiplier));
        }

        public static int ClampLookAheadWaypointCount(int lookAheadWaypointCount)
        {
            return Mathf.Max(1, lookAheadWaypointCount);
        }

        public static float ClampReachDistance(float reachDistance)
        {
            return Mathf.Max(MinimumReachDistance, reachDistance);
        }
    }
}
