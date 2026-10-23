namespace ApexRivals.Career.Runtime
{
    /// <summary>The active objective: the first milestone, in configured order, that is not achieved yet.</summary>
    public readonly struct CareerObjective
    {
        public CareerObjective(string milestoneId, string title, string hint, string progressText, int currencyReward, bool isFinale)
        {
            MilestoneId = milestoneId ?? string.Empty;
            Title = title ?? string.Empty;
            Hint = hint ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            CurrencyReward = currencyReward;
            IsFinale = isFinale;
        }

        public string MilestoneId { get; }
        public string Title { get; }
        public string Hint { get; }

        /// <summary>Short counter such as "1 / 2 CARS"; empty for one-shot goals.</summary>
        public string ProgressText { get; }
        public int CurrencyReward { get; }
        public bool IsFinale { get; }
        public bool HasObjective => !string.IsNullOrEmpty(MilestoneId);
    }
}
