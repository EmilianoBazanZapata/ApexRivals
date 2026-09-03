namespace ApexRivals.Race.Runtime
{
    public readonly struct CountdownChangedEvent
    {
        public CountdownChangedEvent(float remainingSeconds)
        {
            RemainingSeconds = remainingSeconds;
        }

        public float RemainingSeconds { get; }
    }

    public readonly struct RaceStartedEvent
    {
        public RaceStartedEvent(float elapsedTime)
        {
            ElapsedTime = elapsedTime;
        }

        public float ElapsedTime { get; }
    }

    public readonly struct CheckpointPassedEvent
    {
        public CheckpointPassedEvent(string racerId, int checkpointIndex, int currentLap)
        {
            RacerId = racerId;
            CheckpointIndex = checkpointIndex;
            CurrentLap = currentLap;
        }

        public string RacerId { get; }
        public int CheckpointIndex { get; }
        public int CurrentLap { get; }
    }

    public readonly struct LapCompletedEvent
    {
        public LapCompletedEvent(string racerId, int completedLap, int totalLaps)
        {
            RacerId = racerId;
            CompletedLap = completedLap;
            TotalLaps = totalLaps;
        }

        public string RacerId { get; }
        public int CompletedLap { get; }
        public int TotalLaps { get; }
    }

    public readonly struct PositionChangedEvent
    {
        public PositionChangedEvent(string racerId, int position)
        {
            RacerId = racerId;
            Position = position;
        }

        public string RacerId { get; }
        public int Position { get; }
    }

    public readonly struct RacerFinishedEvent
    {
        public RacerFinishedEvent(RaceResult result)
        {
            Result = result;
        }

        public RaceResult Result { get; }
    }

    public readonly struct RaceFinishedEvent
    {
        public RaceFinishedEvent(RaceResult winnerResult)
        {
            WinnerResult = winnerResult;
        }

        public RaceResult WinnerResult { get; }
    }
}
