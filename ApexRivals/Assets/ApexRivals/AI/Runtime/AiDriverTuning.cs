namespace ApexRivals.AI.Runtime
{
    /// <summary>Difficulty-scaled speed limits plus the recovery and input-smoothing values one frame of driving needs.</summary>
    public readonly struct AiDriverTuning
    {
        public AiDriverTuning(
            float targetSpeed,
            float minimumCornerSpeed,
            float stuckSpeedThreshold,
            float stuckDetectionDuration,
            float steeringSmoothing,
            float throttleSmoothing,
            float brakeSmoothing)
        {
            TargetSpeed = targetSpeed;
            MinimumCornerSpeed = minimumCornerSpeed;
            StuckSpeedThreshold = stuckSpeedThreshold;
            StuckDetectionDuration = stuckDetectionDuration;
            SteeringSmoothing = steeringSmoothing;
            ThrottleSmoothing = throttleSmoothing;
            BrakeSmoothing = brakeSmoothing;
        }

        public float TargetSpeed { get; }
        public float MinimumCornerSpeed { get; }
        public float StuckSpeedThreshold { get; }
        public float StuckDetectionDuration { get; }
        public float SteeringSmoothing { get; }
        public float ThrottleSmoothing { get; }
        public float BrakeSmoothing { get; }
    }
}
