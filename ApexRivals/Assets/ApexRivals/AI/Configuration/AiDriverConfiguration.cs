using ApexRivals.AI.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Configuration
{
    [CreateAssetMenu(fileName = "AiDriverConfiguration", menuName = "Apex Rivals/AI/AI Driver Configuration")]
    public sealed class AiDriverConfiguration : ScriptableObject
    {
        [SerializeField]
        private string difficultyName = "Standard";

        [SerializeField, Min(AiDriverRules.MinimumDifficultyMultiplier)]
        private float difficultyMultiplier = 1f;

        [SerializeField, Min(0f)]
        private float targetSpeed = 22f;

        [SerializeField, Min(0f)]
        private float minimumCornerSpeed = 8f;

        [SerializeField, Min(0f)]
        private float steeringSensitivity = 1.1f;

        [SerializeField, Min(0f)]
        private float brakingSensitivity = 1.2f;

        [SerializeField, Range(0f, 1f)]
        private float driftCornerThreshold = 0.65f;

        [SerializeField, Min(0f)]
        private float driftMinimumSpeed = 12f;

        [SerializeField, Min(0f)]
        private float stuckSpeedThreshold = 1.5f;

        [SerializeField, Min(0f)]
        private float stuckDetectionDuration = 2.5f;

        [SerializeField, Min(0f)]
        private float recoveryCooldown = 5f;

        [SerializeField, Min(0f)]
        private float steeringSmoothing = 8f;

        [SerializeField, Min(0f)]
        private float throttleSmoothing = 6f;

        [SerializeField, Min(0f)]
        private float brakeSmoothing = 10f;

        [SerializeField, Min(1)]
        private int lookAheadWaypointCount = 2;

        [SerializeField, Min(AiDriverRules.MinimumReachDistance)]
        private float waypointReachDistance = 5f;

        public string DifficultyName => difficultyName;
        public float DifficultyMultiplier => difficultyMultiplier;
        public int LookAheadWaypointCount => lookAheadWaypointCount;
        public float WaypointReachDistance => waypointReachDistance;

        public AiDriverTuning CreateTuning()
        {
            return new AiDriverTuning(
                AiDriverRules.ApplyDifficulty(targetSpeed, difficultyMultiplier),
                Mathf.Min(minimumCornerSpeed, AiDriverRules.ApplyDifficulty(targetSpeed, difficultyMultiplier)),
                steeringSensitivity,
                brakingSensitivity,
                driftCornerThreshold,
                driftMinimumSpeed,
                stuckSpeedThreshold,
                stuckDetectionDuration,
                recoveryCooldown,
                steeringSmoothing,
                throttleSmoothing,
                brakeSmoothing);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(difficultyName))
            {
                difficultyName = "Standard";
            }

            difficultyMultiplier = Mathf.Max(AiDriverRules.MinimumDifficultyMultiplier, difficultyMultiplier);
            targetSpeed = Mathf.Max(0f, targetSpeed);
            minimumCornerSpeed = Mathf.Clamp(minimumCornerSpeed, 0f, targetSpeed);
            steeringSensitivity = Mathf.Max(0f, steeringSensitivity);
            brakingSensitivity = Mathf.Max(0f, brakingSensitivity);
            driftCornerThreshold = Mathf.Clamp01(driftCornerThreshold);
            driftMinimumSpeed = Mathf.Max(0f, driftMinimumSpeed);
            stuckSpeedThreshold = Mathf.Max(0f, stuckSpeedThreshold);
            stuckDetectionDuration = Mathf.Max(0f, stuckDetectionDuration);
            recoveryCooldown = Mathf.Max(0f, recoveryCooldown);
            steeringSmoothing = Mathf.Max(0f, steeringSmoothing);
            throttleSmoothing = Mathf.Max(0f, throttleSmoothing);
            brakeSmoothing = Mathf.Max(0f, brakeSmoothing);
            lookAheadWaypointCount = AiDriverRules.ClampLookAheadWaypointCount(lookAheadWaypointCount);
            waypointReachDistance = AiDriverRules.ClampReachDistance(waypointReachDistance);
        }
    }
}
