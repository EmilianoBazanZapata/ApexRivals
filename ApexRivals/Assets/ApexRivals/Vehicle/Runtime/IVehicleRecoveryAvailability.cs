namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>Race-owned eligibility for recovering a vehicle outside the road corridor.</summary>
    public interface IVehicleRecoveryAvailability
    {
        bool IsOutsideTrack { get; }
    }
}
