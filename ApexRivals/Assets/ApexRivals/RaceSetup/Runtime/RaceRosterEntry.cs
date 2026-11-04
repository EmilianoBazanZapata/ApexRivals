namespace ApexRivals.RaceSetup.Runtime
{
    public readonly struct RaceRosterEntry
    {
        public RaceRosterEntry(
            string participantId,
            RaceEntryType entryType,
            string vehicleId,
            int startingGridIndex,
            string aiConfigurationId,
            float performanceMultiplier = 1f,
            float handlingMultiplier = 1f)
        {
            ParticipantId = participantId;
            EntryType = entryType;
            VehicleId = vehicleId;
            StartingGridIndex = startingGridIndex;
            AiConfigurationId = aiConfigurationId;
            PerformanceMultiplier = performanceMultiplier > 0f ? performanceMultiplier : 1f;
            HandlingMultiplier = handlingMultiplier > 0f && !float.IsInfinity(handlingMultiplier) ? handlingMultiplier : 1f;
        }

        public string ParticipantId { get; }
        public RaceEntryType EntryType { get; }
        public string VehicleId { get; }
        public int StartingGridIndex { get; }
        public string AiConfigurationId { get; }

        /// <summary>Scales acceleration and top speed of an AI entry's vehicle; 1 keeps its own stats.</summary>
        public float PerformanceMultiplier { get; }
        public float HandlingMultiplier { get; }

        public bool IsPlayer => EntryType == RaceEntryType.Player;
    }
}
