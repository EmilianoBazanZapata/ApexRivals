using System;
using System.Collections.Generic;

namespace ApexRivals.Career.Runtime
{
    /// <summary>The career final: a fixed event on an existing circuit, open once its milestones are achieved.</summary>
    public sealed class CareerFinale
    {
        public CareerFinale(string trackId, string title, string goal, IReadOnlyList<string> requiredMilestoneIds)
        {
            TrackId = trackId ?? string.Empty;
            Title = title ?? string.Empty;
            Goal = goal ?? string.Empty;
            RequiredMilestoneIds = requiredMilestoneIds ?? Array.Empty<string>();
        }

        public string TrackId { get; }
        public string Title { get; }

        /// <summary>Win condition shown to the player, e.g. "Finish 1st".</summary>
        public string Goal { get; }
        public IReadOnlyList<string> RequiredMilestoneIds { get; }
    }
}
