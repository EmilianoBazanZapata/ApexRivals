namespace ApexRivals.Race.Runtime
{
    public readonly struct RaceResult
    {
        public RaceResult(string racerId, int finalPosition, float finishTime)
        {
            RacerId = racerId;
            FinalPosition = finalPosition;
            FinishTime = finishTime;
        }

        public string RacerId { get; }
        public int FinalPosition { get; }
        public float FinishTime { get; }
    }
}
