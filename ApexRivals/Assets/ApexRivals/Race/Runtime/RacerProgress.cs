using System;

namespace ApexRivals.Race.Runtime
{
    public sealed class RacerProgress
    {
        public RacerProgress(string racerId)
        {
            if (string.IsNullOrWhiteSpace(racerId))
            {
                throw new ArgumentException("Racer id cannot be empty.", nameof(racerId));
            }

            RacerId = racerId;
            CurrentLap = 1;
            NextExpectedCheckpointIndex = 0;
            LastValidCheckpointIndex = -1;
            FinalPosition = 0;
        }

        public string RacerId { get; }
        public int CurrentLap { get; private set; }
        public int CompletedLaps { get; private set; }
        public int NextExpectedCheckpointIndex { get; private set; }
        public int LastValidCheckpointIndex { get; private set; }
        public int TotalPassedCheckpoints { get; private set; }
        public float FinishTime { get; private set; }
        public bool HasFinished { get; private set; }
        public int FinalPosition { get; private set; }

        public CheckpointPassResult PassCheckpoint(int checkpointIndex, int checkpointCount, int totalLaps, float elapsedTime, int nextFinishPosition)
        {
            if (HasFinished)
            {
                return new CheckpointPassResult(CheckpointPassStatus.RacerAlreadyFinished, false, false);
            }

            if (checkpointIndex < 0 || checkpointIndex >= checkpointCount)
            {
                return new CheckpointPassResult(CheckpointPassStatus.InvalidCheckpoint, false, false);
            }

            if (checkpointIndex == LastValidCheckpointIndex)
            {
                return new CheckpointPassResult(CheckpointPassStatus.Duplicate, false, false);
            }

            if (checkpointIndex != NextExpectedCheckpointIndex)
            {
                return new CheckpointPassResult(CheckpointPassStatus.Skipped, false, false);
            }

            LastValidCheckpointIndex = checkpointIndex;
            TotalPassedCheckpoints++;

            var lapCompleted = checkpointIndex == checkpointCount - 1;
            if (!lapCompleted)
            {
                NextExpectedCheckpointIndex = checkpointIndex + 1;
                return new CheckpointPassResult(CheckpointPassStatus.Accepted, false, false);
            }

            CompletedLaps++;
            NextExpectedCheckpointIndex = 0;
            LastValidCheckpointIndex = -1;

            if (CompletedLaps >= totalLaps)
            {
                HasFinished = true;
                FinishTime = elapsedTime;
                FinalPosition = nextFinishPosition;
                CurrentLap = totalLaps;
                return new CheckpointPassResult(CheckpointPassStatus.Accepted, true, true);
            }

            CurrentLap = CompletedLaps + 1;
            return new CheckpointPassResult(CheckpointPassStatus.Accepted, true, false);
        }
    }
}
