namespace ApexRivals.Vehicle.Runtime
{
    public readonly struct GearShiftDecision
    {
        public GearShiftDecision(int requestedGearIndex, bool shouldShift)
        {
            RequestedGearIndex = requestedGearIndex;
            ShouldShift = shouldShift;
        }

        public int RequestedGearIndex { get; }
        public bool ShouldShift { get; }
    }
}
