using ApexRivals.Career.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;

namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct RaceSessionResult
    {
        private readonly CareerRaceOutcome _careerOutcome;

        public RaceSessionResult(RaceResult raceResult, int earnedReward)
            : this(raceResult, earnedReward, 0, 0, 0, default, default, false, true, string.Empty)
        {
        }

        public RaceSessionResult(
            RaceResult raceResult,
            int earnedReward,
            int totalParticipants,
            int updatedCurrency,
            bool saveSucceeded,
            string saveMessage)
            : this(raceResult, earnedReward, totalParticipants, updatedCurrency, 0, default, default, false, saveSucceeded, saveMessage)
        {
        }

        public RaceSessionResult(
            RaceResult raceResult,
            int earnedReward,
            int totalParticipants,
            int updatedCurrency,
            int completedRaceCount,
            RaceProgressionTier previousTier,
            RaceProgressionTier currentTier,
            bool newTierReached,
            bool saveSucceeded,
            string saveMessage,
            CareerRaceOutcome careerOutcome = null)
        {
            _careerOutcome = careerOutcome;
            RaceResult = raceResult;
            EarnedReward = earnedReward;
            TotalParticipants = totalParticipants;
            UpdatedCurrency = updatedCurrency;
            CompletedRaceCount = completedRaceCount;
            PreviousTier = previousTier;
            CurrentTier = currentTier;
            NewTierReached = newTierReached;
            SaveSucceeded = saveSucceeded;
            SaveMessage = saveMessage;
        }

        public RaceResult RaceResult { get; }
        public int EarnedReward { get; }
        public int TotalParticipants { get; }
        public int UpdatedCurrency { get; }
        public int CompletedRaceCount { get; }
        public RaceProgressionTier PreviousTier { get; }
        public RaceProgressionTier CurrentTier { get; }
        public bool NewTierReached { get; }
        public bool SaveSucceeded { get; }
        public string SaveMessage { get; }

        /// <summary>Career changes made by this race; <see cref="CareerRaceOutcome.None"/> without a career.</summary>
        public CareerRaceOutcome CareerOutcome => _careerOutcome ?? CareerRaceOutcome.None;
        public int FinalPosition => RaceResult.FinalPosition;
        public float FinishTime => RaceResult.FinishTime;
    }
}
