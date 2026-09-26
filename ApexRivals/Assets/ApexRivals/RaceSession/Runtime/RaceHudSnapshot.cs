namespace ApexRivals.RaceSession.Runtime
{
    public readonly struct RaceHudSnapshot
    {
        public RaceHudSnapshot(
            int currentLap,
            int totalLaps,
            int currentPosition,
            int participantCount,
            float countdownRemaining,
            float raceTime)
        {
            CurrentLap = currentLap;
            TotalLaps = totalLaps;
            CurrentPosition = currentPosition;
            ParticipantCount = participantCount;
            CountdownRemaining = countdownRemaining;
            RaceTime = raceTime;
        }

        public int CurrentLap { get; }
        public int TotalLaps { get; }
        public int CurrentPosition { get; }
        public int ParticipantCount { get; }
        public float CountdownRemaining { get; }
        public float RaceTime { get; }
    }
}
