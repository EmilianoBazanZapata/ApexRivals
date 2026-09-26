using System;

namespace ApexRivals.Race.Runtime
{
    public sealed class RacerProgress
    {
        public RacerProgress(string racerId)
            : this(racerId, 0, -1)
        {
        }

        public RacerProgress(string racerId, int checkpointCount, int initiallyConsumedCheckpointIndex)
        {
            if (string.IsNullOrWhiteSpace(racerId))
            {
                throw new ArgumentException("Racer id cannot be empty.", nameof(racerId));
            }

            RacerId = racerId;
            CurrentLap = 1;
            if (IsValidCheckpointIndex(initiallyConsumedCheckpointIndex, checkpointCount))
            {
                // A start grid can sit on the start/finish trigger. Consume that
                // checkpoint as setup state, rather than waiting for a trigger that
                // cannot fire until the racer leaves and re-enters the volume.
                LastValidCheckpointIndex = initiallyConsumedCheckpointIndex;
                NextExpectedCheckpointIndex = GetNextCheckpointIndex(initiallyConsumedCheckpointIndex, checkpointCount);
                _lapCompletionCheckpointIndex = initiallyConsumedCheckpointIndex;
            }
            else
            {
                NextExpectedCheckpointIndex = 0;
                LastValidCheckpointIndex = -1;
                _lapCompletionCheckpointIndex = -1;
            }

            FinalPosition = 0;
        }

        private readonly int _lapCompletionCheckpointIndex;

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

            var lapCompleted = checkpointIndex == GetLapCompletionCheckpointIndex(checkpointCount);
            if (!lapCompleted)
            {
                NextExpectedCheckpointIndex = GetNextCheckpointIndex(checkpointIndex, checkpointCount);
                return new CheckpointPassResult(CheckpointPassStatus.Accepted, false, false);
            }

            CompletedLaps++;
            NextExpectedCheckpointIndex = GetNextCheckpointIndex(checkpointIndex, checkpointCount);
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

        private int GetLapCompletionCheckpointIndex(int checkpointCount)
        {
            return _lapCompletionCheckpointIndex >= 0
                ? _lapCompletionCheckpointIndex
                : checkpointCount - 1;
        }

        private static int GetNextCheckpointIndex(int checkpointIndex, int checkpointCount)
        {
            return (checkpointIndex + 1) % checkpointCount;
        }

        private static bool IsValidCheckpointIndex(int checkpointIndex, int checkpointCount)
        {
            return checkpointCount > 1 && checkpointIndex >= 0 && checkpointIndex < checkpointCount;
        }
    }
}
