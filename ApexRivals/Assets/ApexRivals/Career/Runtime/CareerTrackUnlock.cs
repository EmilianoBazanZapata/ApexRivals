namespace ApexRivals.Career.Runtime
{
    /// <summary>Milestone that opens a circuit. Circuits without an entry are available from the start.</summary>
    public sealed class CareerTrackUnlock
    {
        public CareerTrackUnlock(string trackId, string requiredMilestoneId)
        {
            TrackId = trackId ?? string.Empty;
            RequiredMilestoneId = requiredMilestoneId ?? string.Empty;
        }

        public string TrackId { get; }
        public string RequiredMilestoneId { get; }
    }
}
