using System;
using System.Collections.Generic;

namespace ApexRivals.Career.Runtime
{
    public readonly struct CareerVehiclePurchaseResult
    {
        public CareerVehiclePurchaseResult(
            CareerVehiclePurchaseStatus status,
            string vehicleId,
            int price,
            int currency,
            IReadOnlyList<CareerMilestoneAward> awards,
            string message)
        {
            Status = status;
            VehicleId = vehicleId ?? string.Empty;
            Price = price;
            Currency = currency;
            Awards = awards ?? Array.Empty<CareerMilestoneAward>();
            Message = message ?? string.Empty;
        }

        public CareerVehiclePurchaseStatus Status { get; }
        public string VehicleId { get; }
        public int Price { get; }

        /// <summary>Wallet balance after the request (unchanged unless the purchase went through).</summary>
        public int Currency { get; }
        public IReadOnlyList<CareerMilestoneAward> Awards { get; }
        public string Message { get; }
        public bool Succeeded => Status == CareerVehiclePurchaseStatus.Purchased;
    }
}
