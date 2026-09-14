namespace ApexRivals.Race.Runtime
{
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
}
