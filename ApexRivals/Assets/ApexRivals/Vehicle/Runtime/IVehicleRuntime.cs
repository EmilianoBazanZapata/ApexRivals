namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Narrow gameplay boundary shared by production vehicle runtimes.
    /// It deliberately excludes controller-specific physics diagnostics and tuning details.
    /// </summary>
    public interface IVehicleRuntime
    {
        float CurrentForwardSpeed { get; }
        VehiclePerformanceStats CurrentPerformanceStats { get; }

        void ConfigureRuntime(IDrivingInputProvider inputProvider, VehicleResetter vehicleResetter);
        void ApplyPerformanceStats(VehiclePerformanceStats performanceStats);
    }
}
