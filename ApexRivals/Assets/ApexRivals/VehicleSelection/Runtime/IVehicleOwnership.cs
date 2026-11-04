namespace ApexRivals.VehicleSelection.Runtime
{
    /// <summary>Answers whether the player may race a car. Without one, every catalog car is usable.</summary>
    public interface IVehicleOwnership
    {
        bool IsVehicleOwned(string vehicleId);
    }
}
