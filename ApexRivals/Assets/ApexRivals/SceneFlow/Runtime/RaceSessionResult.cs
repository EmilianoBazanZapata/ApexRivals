using ApexRivals.Race.Runtime;

namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct RaceSessionResult
    {
        public RaceSessionResult(RaceResult raceResult, int earnedReward)
        {
            RaceResult = raceResult;
            EarnedReward = earnedReward;
        }

        public RaceResult RaceResult { get; }
        public int EarnedReward { get; }
        public int FinalPosition => RaceResult.FinalPosition;
        public float FinishTime => RaceResult.FinishTime;
    }
}
