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
            bool saveSucceeded,
            string saveMessage)
        {
            RaceResult = raceResult;
            TotalParticipants = totalParticipants;
            EarnedReward = earnedReward;
            UpdatedCurrency = updatedCurrency;
            SaveSucceeded = saveSucceeded;
            SaveMessage = saveMessage;
        }

        public RaceResult RaceResult { get; }
        public int FinalPosition => RaceResult.FinalPosition;
        public float RaceTime => RaceResult.FinishTime;
        public int TotalParticipants { get; }
        public int EarnedReward { get; }
        public int UpdatedCurrency { get; }
        public bool SaveSucceeded { get; }
        public string SaveMessage { get; }
    }
}
