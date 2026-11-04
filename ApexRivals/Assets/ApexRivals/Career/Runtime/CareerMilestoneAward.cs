namespace ApexRivals.Career.Runtime
{
    /// <summary>A milestone achieved just now and the bonus actually credited for it.</summary>
    public readonly struct CareerMilestoneAward
    {
        public CareerMilestoneAward(string milestoneId, string title, int creditedReward, bool isFinale)
        {
            MilestoneId = milestoneId;
            Title = title;
            CreditedReward = creditedReward;
            IsFinale = isFinale;
        }

        public string MilestoneId { get; }
        public string Title { get; }
        public int CreditedReward { get; }
        public bool IsFinale { get; }
    }
}
