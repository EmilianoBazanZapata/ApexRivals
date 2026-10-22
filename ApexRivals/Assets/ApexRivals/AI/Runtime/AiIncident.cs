namespace ApexRivals.AI.Runtime
{
    public enum AiIncidentKind
    {
        OffTrack,
        Reverse,
        Reset
    }

    /// <summary>A recovery event recorded for debugging and tuning: where it happened and how fast the car was going.</summary>
    public readonly struct AiIncident
    {
        public AiIncident(AiIncidentKind kind, float lapDistance, float speed, float targetSpeed, float raceTime)
        {
            Kind = kind;
            LapDistance = lapDistance;
            Speed = speed;
            TargetSpeed = targetSpeed;
            RaceTime = raceTime;
        }

        public AiIncidentKind Kind { get; }
        public float LapDistance { get; }
        public float Speed { get; }
        public float TargetSpeed { get; }
        public float RaceTime { get; }
    }
}
