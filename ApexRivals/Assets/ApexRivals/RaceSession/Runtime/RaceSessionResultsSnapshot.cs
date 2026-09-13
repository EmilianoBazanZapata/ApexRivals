using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;

namespace ApexRivals.RaceSession.Runtime
{
    public readonly struct RaceSessionResultsSnapshot
    {
        public RaceSessionResultsSnapshot(
            RaceResult raceResult,
            int totalParticipants,
            int earnedReward,
            int updatedCurrency,
            int completedRaceCount,
            RaceProgressionTier previousTier,
            RaceProgressionTier currentTier,
            bool newTierReached,
            bool saveSucceeded,
            string saveMessage)
        {
            RaceResult = raceResult;
            TotalParticipants = totalParticipants;
            EarnedReward = earnedReward;
            UpdatedCurrency = updatedCurrency;
            CompletedRaceCount = completedRaceCount;
            PreviousTier = previousTier;
            CurrentTier = currentTier;
            NewTierReached = newTierReached;
            SaveSucceeded = saveSucceeded;
            SaveMessage = saveMessage;
        }

        public RaceResult RaceResult { get; }
        public int FinalPosition => RaceResult.FinalPosition;
        public float RaceTime => RaceResult.FinishTime;
        public int TotalParticipants { get; }
        public int EarnedReward { get; }
        public int UpdatedCurrency { get; }
        public int CompletedRaceCount { get; }
        public RaceProgressionTier PreviousTier { get; }
        public RaceProgressionTier CurrentTier { get; }
        public bool NewTierReached { get; }
        public bool SaveSucceeded { get; }
        public string SaveMessage { get; }
    }
}
