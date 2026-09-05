using ApexRivals.Race.Runtime;

namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct RaceSessionResult
    {
        public RaceSessionResult(RaceResult raceResult, int earnedReward)
            : this(raceResult, earnedReward, 0, 0, true, string.Empty)
        {
        }

        public RaceSessionResult(
            RaceResult raceResult,
            int earnedReward,
            int totalParticipants,
            int updatedCurrency,
            bool saveSucceeded,
            string saveMessage)
        {
            RaceResult = raceResult;
            EarnedReward = earnedReward;
            TotalParticipants = totalParticipants;
            UpdatedCurrency = updatedCurrency;
            SaveSucceeded = saveSucceeded;
            SaveMessage = saveMessage;
        }

        public RaceResult RaceResult { get; }
        public int EarnedReward { get; }
        public int TotalParticipants { get; }
        public int UpdatedCurrency { get; }
        public bool SaveSucceeded { get; }
        public string SaveMessage { get; }
        public int FinalPosition => RaceResult.FinalPosition;
        public float FinishTime => RaceResult.FinishTime;
    }
}
