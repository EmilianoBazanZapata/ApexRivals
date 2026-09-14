using System;

namespace ApexRivals.Garage.Runtime
{
    public readonly struct UpgradeLevel
    {
        public UpgradeLevel(int price, UpgradeStatModifier statModifier)
        {
            if (price < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price), "Upgrade price cannot be negative.");
            }

            Price = price;
            StatModifier = statModifier;
        }

        public int Price { get; }
        public UpgradeStatModifier StatModifier { get; }
    }
}
