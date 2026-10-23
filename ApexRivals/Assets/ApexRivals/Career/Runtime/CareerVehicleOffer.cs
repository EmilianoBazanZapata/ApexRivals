namespace ApexRivals.Career.Runtime
{
    /// <summary>Price and prerequisite for buying a car that is not the starter.</summary>
    public sealed class CareerVehicleOffer
    {
        public CareerVehicleOffer(string vehicleId, int price, string requiredMilestoneId)
        {
            VehicleId = vehicleId ?? string.Empty;
            Price = price;
            RequiredMilestoneId = requiredMilestoneId ?? string.Empty;
        }

        public string VehicleId { get; }
        public int Price { get; }

        /// <summary>Empty when the car can be bought from the start.</summary>
        public string RequiredMilestoneId { get; }
    }
}
