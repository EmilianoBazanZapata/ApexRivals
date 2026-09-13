namespace ApexRivals.Race.Runtime
{
    public readonly struct CheckpointPassResult
    {
        public CheckpointPassResult(CheckpointPassStatus status, bool lapCompleted, bool racerFinished)
        {
            Status = status;
            LapCompleted = lapCompleted;
            RacerFinished = racerFinished;
        }

        public CheckpointPassStatus Status { get; }
        public bool LapCompleted { get; }
        public bool RacerFinished { get; }
        public bool Accepted => Status == CheckpointPassStatus.Accepted;
    }
}
