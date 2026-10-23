namespace ApexRivals.Career.Runtime
{
    /// <summary>One completion milestone. Its ID is stable and is what saves record once it is achieved.</summary>
    public sealed class CareerMilestone
    {
        public CareerMilestone(
            string milestoneId,
            string title,
            string hint,
            CareerMilestoneKind kind,
            string targetId,
            int targetCount,
            int currencyReward)
        {
            MilestoneId = milestoneId ?? string.Empty;
            Title = title ?? string.Empty;
            Hint = hint ?? string.Empty;
            Kind = kind;
            TargetId = targetId ?? string.Empty;
            TargetCount = targetCount;
            CurrencyReward = currencyReward;
        }

        public string MilestoneId { get; }
        public string Title { get; }

        /// <summary>What the player should do next to achieve it.</summary>
        public string Hint { get; }
        public CareerMilestoneKind Kind { get; }
        public string TargetId { get; }
        public int TargetCount { get; }

        /// <summary>One-time bonus credited when the milestone is first achieved.</summary>
        public int CurrencyReward { get; }
    }
}
