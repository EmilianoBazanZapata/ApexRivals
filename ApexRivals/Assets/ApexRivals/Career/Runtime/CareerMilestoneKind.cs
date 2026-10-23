namespace ApexRivals.Career.Runtime
{
    /// <summary>The small, closed set of career objective types. Each one reads a single persisted fact.</summary>
    public enum CareerMilestoneKind
    {
        /// <summary>Finish any race on <see cref="CareerMilestone.TargetId"/> (a track ID).</summary>
        CompleteTrack,

        /// <summary>Finish 1st in a race on <see cref="CareerMilestone.TargetId"/> (a track ID).</summary>
        WinTrack,

        /// <summary>Reach upgrade level <see cref="CareerMilestone.TargetCount"/> on the upgrade named by <see cref="CareerMilestone.TargetId"/>.</summary>
        ReachUpgradeLevel,

        /// <summary>Own at least <see cref="CareerMilestone.TargetCount"/> cars, the starter included.</summary>
        OwnVehicles,

        /// <summary>Have at least <see cref="CareerMilestone.TargetCount"/> circuits unlocked, the initial one included.</summary>
        UnlockTracks,

        /// <summary>Win the career final.</summary>
        WinFinale
    }
}
