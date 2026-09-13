namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Provides implementation-neutral performance values for selection, garage, and race setup.
    /// </summary>
    public interface IVehiclePerformanceStatsSource
    {
        VehiclePerformanceStats CreatePerformanceStats();
    }
}
