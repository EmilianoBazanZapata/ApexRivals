namespace ApexRivals.Career.Runtime
{
    public readonly struct CareerTrackStatus
    {
        public CareerTrackStatus(string trackId, bool known, bool unlocked, string requirementTitle, bool completed, bool won)
        {
            TrackId = trackId ?? string.Empty;
            Known = known;
            Unlocked = unlocked;
            RequirementTitle = requirementTitle ?? string.Empty;
            Completed = completed;
            Won = won;
        }

        public string TrackId { get; }
        public bool Known { get; }
        public bool Unlocked { get; }

        /// <summary>Milestone that opens the circuit; empty for circuits open from the start.</summary>
        public string RequirementTitle { get; }
        public bool Completed { get; }
        public bool Won { get; }
    }
}
