namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Narrow, read-only vehicle telemetry boundary for presentation (HUD) consumers.
    /// Kept separate from IVehicleRuntime, which is deliberately scoped to gameplay/AI
    /// runtime wiring and excludes controller-specific diagnostics - see IVehicleRuntime.
    /// </summary>
    public interface IVehicleTelemetry
    {
        float SpeedKph { get; }
        int CurrentGear { get; }
        float EngineRpm { get; }
        bool IsReversing { get; }
    }
}
