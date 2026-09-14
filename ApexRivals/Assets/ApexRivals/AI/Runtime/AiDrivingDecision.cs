namespace ApexRivals.AI.Runtime
{
    public readonly struct AiDrivingDecision
    {
        public AiDrivingDecision(float throttle, float brake, float steering, bool drift)
        {
            Throttle = throttle;
            Brake = brake;
            Steering = steering;
            Drift = drift;
        }

        public float Throttle { get; }
        public float Brake { get; }
        public float Steering { get; }
        public bool Drift { get; }
    }
}
