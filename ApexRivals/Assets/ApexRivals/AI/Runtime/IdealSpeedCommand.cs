namespace ApexRivals.AI.Runtime
{
    public readonly struct IdealSpeedCommand
    {
        public IdealSpeedCommand(float throttle, float brake)
        {
            Throttle = throttle;
            Brake = brake;
        }

        public float Throttle { get; }
        public float Brake { get; }
    }
}
