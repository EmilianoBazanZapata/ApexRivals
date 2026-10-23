namespace ApexRivals.Career.Runtime
{
    public readonly struct CareerFinaleStatus
    {
        public CareerFinaleStatus(
            CareerFinaleState state,
            string title,
            string goal,
            string trackId,
            int metRequirements,
            int totalRequirements,
            string nextMissingTitle)
        {
            State = state;
            Title = title ?? string.Empty;
            Goal = goal ?? string.Empty;
            TrackId = trackId ?? string.Empty;
            MetRequirements = metRequirements;
            TotalRequirements = totalRequirements;
            NextMissingTitle = nextMissingTitle ?? string.Empty;
        }

        public CareerFinaleState State { get; }
        public string Title { get; }
        public string Goal { get; }
        public string TrackId { get; }
        public int MetRequirements { get; }
        public int TotalRequirements { get; }

        /// <summary>First requirement still missing, in configuration order; empty once qualified.</summary>
        public string NextMissingTitle { get; }
        public bool Qualified => State != CareerFinaleState.Locked;
    }
}
