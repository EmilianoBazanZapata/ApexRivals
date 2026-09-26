namespace ApexRivals.AI.Runtime
{
    public readonly struct AiDriverTuning
    {
        public AiDriverTuning(
            float targetSpeed,
            float minimumCornerSpeed,
            float steeringSensitivity,
            float brakingSensitivity,
            float driftCornerThreshold,
            float driftMinimumSpeed,
            float stuckSpeedThreshold,
            float stuckDetectionDuration,
            float recoveryCooldown,
            float steeringSmoothing,
            float throttleSmoothing,
            float brakeSmoothing)
        {
            TargetSpeed = targetSpeed;
            MinimumCornerSpeed = minimumCornerSpeed;
            SteeringSensitivity = steeringSensitivity;
            BrakingSensitivity = brakingSensitivity;
            DriftCornerThreshold = driftCornerThreshold;
            DriftMinimumSpeed = driftMinimumSpeed;
            StuckSpeedThreshold = stuckSpeedThreshold;
            StuckDetectionDuration = stuckDetectionDuration;
            RecoveryCooldown = recoveryCooldown;
            SteeringSmoothing = steeringSmoothing;
            ThrottleSmoothing = throttleSmoothing;
            BrakeSmoothing = brakeSmoothing;
        }

        public float TargetSpeed { get; }
        public float MinimumCornerSpeed { get; }
        public float SteeringSensitivity { get; }
        public float BrakingSensitivity { get; }
        public float DriftCornerThreshold { get; }
        public float DriftMinimumSpeed { get; }
        public float StuckSpeedThreshold { get; }
        public float StuckDetectionDuration { get; }
        public float RecoveryCooldown { get; }
        public float SteeringSmoothing { get; }
        public float ThrottleSmoothing { get; }
        public float BrakeSmoothing { get; }
    }
}
