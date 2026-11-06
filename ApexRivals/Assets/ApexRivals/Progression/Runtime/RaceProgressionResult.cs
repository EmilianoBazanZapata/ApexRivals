using System;

namespace ApexRivals.Progression.Runtime
{
    public readonly struct RaceProgressionResult
    {
        public RaceProgressionResult(
            bool succeeded,
            int previousCompletedRaceCount,
            int completedRaceCount,
            RaceProgressionTier previousTier,
            RaceProgressionTier currentTier,
            string message)
        {
            Succeeded = succeeded;
            PreviousCompletedRaceCount = previousCompletedRaceCount;
            CompletedRaceCount = completedRaceCount;
            PreviousTier = previousTier;
            CurrentTier = currentTier;
            Message = message;
        }

        public bool Succeeded { get; }
        public int PreviousCompletedRaceCount { get; }
        public int CompletedRaceCount { get; }
        public RaceProgressionTier PreviousTier { get; }
        public RaceProgressionTier CurrentTier { get; }
        public string Message { get; }
        public bool NewTierReached => Succeeded && !string.Equals(PreviousTier.TierId, CurrentTier.TierId, StringComparison.Ordinal);

        public static RaceProgressionResult Failure(int completedRaceCount, string message)
        {
            return new RaceProgressionResult(false, completedRaceCount, completedRaceCount, default, default, message);
        }
    }
}
