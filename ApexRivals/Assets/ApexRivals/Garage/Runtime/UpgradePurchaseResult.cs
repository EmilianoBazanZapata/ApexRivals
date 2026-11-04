using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.Garage.Runtime
{
    public readonly struct UpgradePurchaseResult
    {
        public UpgradePurchaseResult(
            UpgradePurchaseStatus status,
            UpgradeType upgradeType,
            int level,
            int price,
            int currency,
            VehiclePerformanceStats effectiveStats)
        {
            Status = status;
            UpgradeType = upgradeType;
            Level = level;
            Price = price;
            Currency = currency;
            EffectiveStats = effectiveStats;
        }

        public UpgradePurchaseStatus Status { get; }
        public UpgradeType UpgradeType { get; }
        public int Level { get; }
        public int Price { get; }
        public int Currency { get; }
        public VehiclePerformanceStats EffectiveStats { get; }
        public bool Succeeded => Status == UpgradePurchaseStatus.Succeeded;
    }
}
