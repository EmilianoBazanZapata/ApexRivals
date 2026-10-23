using System;

namespace ApexRivals.SaveSystem.Runtime
{
    /// <summary>
    /// Career facts in the profile (schema 4+). Only facts are stored: unlocks, the active objective and
    /// completion are derived from them on load.
    /// </summary>
    [Serializable]
    public sealed class CareerSaveData
    {
        public string[] ownedVehicleIds = Array.Empty<string>();
        public string[] completedTrackIds = Array.Empty<string>();
        public string[] wonTrackIds = Array.Empty<string>();

        /// <summary>Achieved milestones; doubles as the ledger of one-time rewards already paid.</summary>
        public string[] achievedMilestoneIds = Array.Empty<string>();
        public bool finaleWon;
    }
}
