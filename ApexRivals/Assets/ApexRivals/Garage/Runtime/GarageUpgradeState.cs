using ApexRivals.Progression.Runtime;

namespace ApexRivals.Garage.Runtime
{
    public readonly struct GarageUpgradeState
    {
        public GarageUpgradeState(UpgradeType upgradeType, int currentLevel, int maximumLevel, int nextPrice, bool canPurchase, UpgradeStatModifier benefitModifier)
        {
            UpgradeType = upgradeType;
            CurrentLevel = currentLevel;
            MaximumLevel = maximumLevel;
            NextPrice = nextPrice;
            CanPurchase = canPurchase;
            BenefitModifier = benefitModifier;
        }

        public UpgradeType UpgradeType { get; }
        public int CurrentLevel { get; }
        public int MaximumLevel { get; }
        public int NextPrice { get; }
        public bool CanPurchase { get; }
        public UpgradeStatModifier BenefitModifier { get; }
        public bool IsAtMaximum => CurrentLevel >= MaximumLevel;
    }
}
