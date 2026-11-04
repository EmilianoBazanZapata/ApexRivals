namespace ApexRivals.Career.Runtime
{
    /// <summary>Ownership, price and prerequisite of one car as the career sees it right now.</summary>
    public readonly struct CareerVehicleStatus
    {
        public CareerVehicleStatus(
            string vehicleId,
            bool isStarter,
            bool owned,
            bool forSale,
            int price,
            bool prerequisiteMet,
            string prerequisiteTitle,
            bool canAfford)
        {
            VehicleId = vehicleId ?? string.Empty;
            IsStarter = isStarter;
            Owned = owned;
            ForSale = forSale;
            Price = price;
            PrerequisiteMet = prerequisiteMet;
            PrerequisiteTitle = prerequisiteTitle ?? string.Empty;
            CanAfford = canAfford;
        }

        public string VehicleId { get; }
        public bool IsStarter { get; }
        public bool Owned { get; }
        public bool ForSale { get; }
        public int Price { get; }
        public bool PrerequisiteMet { get; }

        /// <summary>Title of the milestone that must be achieved before buying; empty when there is none.</summary>
        public string PrerequisiteTitle { get; }
        public bool CanAfford { get; }
        public bool CanPurchase => !Owned && ForSale && PrerequisiteMet && CanAfford;
    }
}
