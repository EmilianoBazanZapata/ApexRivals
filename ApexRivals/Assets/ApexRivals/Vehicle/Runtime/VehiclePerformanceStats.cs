namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Game-facing performance values used by selection, garage upgrades, and vehicle runtimes.
    /// These are not a vehicle-physics configuration format.
    /// </summary>
    public readonly struct VehiclePerformanceStats
    {
        public VehiclePerformanceStats(
            float acceleration,
            float topSpeed,
            float steering,
            float handling,
            float driftHandling)
        {
            Acceleration = acceleration;
            TopSpeed = topSpeed;
            Steering = steering;
            Handling = handling;
            DriftHandling = driftHandling;
        }

        public float Acceleration { get; }
        public float TopSpeed { get; }
        public float Steering { get; }
        public float Handling { get; }
        public float DriftHandling { get; }
    }
}
