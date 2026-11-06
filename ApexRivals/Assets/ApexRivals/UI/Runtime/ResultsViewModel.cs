namespace ApexRivals.UI.Runtime
{
    public readonly struct ResultsViewModel
    {
        public ResultsViewModel(
            int finalPosition,
            int participantCount,
            float raceTime,
            int earnedReward,
            int updatedCurrency,
            int completedRaceCount,
            string previousTierId,
            string previousTierDisplayName,
            string currentTierId,
            string currentTierDisplayName,
            bool newTierReached,
            bool isTransitioning,
            PresentationFailure failure,
            string messageKey)
        {
            FinalPosition = finalPosition;
            ParticipantCount = participantCount;
            RaceTime = raceTime;
            EarnedReward = earnedReward;
            UpdatedCurrency = updatedCurrency;
            CompletedRaceCount = completedRaceCount;
            PreviousTierId = previousTierId;
            PreviousTierDisplayName = previousTierDisplayName;
            CurrentTierId = currentTierId;
            CurrentTierDisplayName = currentTierDisplayName;
            NewTierReached = newTierReached;
            IsTransitioning = isTransitioning;
            Failure = failure;
            MessageKey = messageKey;
        }

        public int FinalPosition { get; }
        public int ParticipantCount { get; }
        public float RaceTime { get; }
        public int EarnedReward { get; }
        public int UpdatedCurrency { get; }
        public int CompletedRaceCount { get; }
        public string PreviousTierId { get; }
        public string PreviousTierDisplayName { get; }
        public string CurrentTierId { get; }
        public string CurrentTierDisplayName { get; }
        public bool NewTierReached { get; }
        public bool IsTransitioning { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }
}
