namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>Refreshes and validates a recovery destination before the reset authority teleports.</summary>
    public interface IVehicleRecoveryPoseProvider
    {
        bool TryPrepareRecoveryPose();
    }
}
