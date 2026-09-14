namespace ApexRivals.RaceSetup.Runtime
{
    public readonly struct RaceRosterEntry
    {
        public RaceRosterEntry(
            string participantId,
            RaceEntryType entryType,
            string vehicleId,
            int startingGridIndex,
            string aiConfigurationId)
        {
            ParticipantId = participantId;
            EntryType = entryType;
            VehicleId = vehicleId;
            StartingGridIndex = startingGridIndex;
            AiConfigurationId = aiConfigurationId;
        }

        public string ParticipantId { get; }
        public RaceEntryType EntryType { get; }
        public string VehicleId { get; }
        public int StartingGridIndex { get; }
        public string AiConfigurationId { get; }
        public bool IsPlayer => EntryType == RaceEntryType.Player;
    }
}
