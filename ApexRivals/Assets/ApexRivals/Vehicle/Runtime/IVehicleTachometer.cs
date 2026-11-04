namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>Optional companion to <see cref="IVehicleTelemetry"/>: the engine's redline, for scaling a tachometer.</summary>
    public interface IVehicleTachometer
    {
        float RedlineRpm { get; }
    }
}
