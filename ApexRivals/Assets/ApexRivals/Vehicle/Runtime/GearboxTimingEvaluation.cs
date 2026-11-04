namespace ApexRivals.Vehicle.Runtime
{
    public readonly struct GearboxTimingEvaluation
    {
        public GearboxTimingEvaluation(float shiftTimeRemaining, float shiftCooldownRemaining, bool canEvaluateGearSelection)
        {
            ShiftTimeRemaining = shiftTimeRemaining;
            ShiftCooldownRemaining = shiftCooldownRemaining;
            CanEvaluateGearSelection = canEvaluateGearSelection;
        }

        public float ShiftTimeRemaining { get; }
        public float ShiftCooldownRemaining { get; }
        public bool CanEvaluateGearSelection { get; }
    }
}
