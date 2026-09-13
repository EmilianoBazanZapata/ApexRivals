namespace ApexRivals.Vehicle.Runtime
{
    public readonly struct EngineRpmEvaluation
    {
        public EngineRpmEvaluation(float targetEngineRpm, float engineRpm)
        {
            TargetEngineRpm = targetEngineRpm;
            EngineRpm = engineRpm;
        }

        public float TargetEngineRpm { get; }
        public float EngineRpm { get; }
    }
}
