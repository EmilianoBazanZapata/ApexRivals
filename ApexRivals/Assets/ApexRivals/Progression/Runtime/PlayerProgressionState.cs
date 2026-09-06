using System;
using System.Collections.Generic;

namespace ApexRivals.Progression.Runtime
{
    public sealed class PlayerProgressionState
    {
        private readonly Dictionary<UpgradeType, int> _upgradeLevels = new Dictionary<UpgradeType, int>();

        public PlayerProgressionState(int startingCurrency = 0, int completedRaceCount = 0)
        {
            if (startingCurrency < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingCurrency), "Starting currency cannot be negative.");
            }

            if (completedRaceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedRaceCount), "Completed race count cannot be negative.");
            }

            Currency = startingCurrency;
            CompletedRaceCount = completedRaceCount;
            _upgradeLevels[UpgradeType.Engine] = 0;
            _upgradeLevels[UpgradeType.Handling] = 0;
        }

        public event Action<ProgressionStateChangedEvent> Changed;

        public int Currency { get; private set; }
        public int CompletedRaceCount { get; private set; }

        public int GetUpgradeLevel(UpgradeType upgradeType)
        {
            return _upgradeLevels.TryGetValue(upgradeType, out var level) ? level : 0;
        }

        public CurrencyTransactionResult AddCurrency(int amount)
        {
            if (amount <= 0)
            {
                return new CurrencyTransactionResult(CurrencyTransactionStatus.InvalidAmount, Currency);
            }

            Currency += amount;
            RaiseChanged();
            return new CurrencyTransactionResult(CurrencyTransactionStatus.Succeeded, Currency);
        }

        public void SetCurrency(int currency)
        {
            if (currency < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currency), "Currency cannot be negative.");
            }

            if (Currency == currency)
            {
                return;
            }

            Currency = currency;
            RaiseChanged();
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && Currency >= amount;
        }

        public CurrencyTransactionResult SpendCurrency(int amount)
        {
            if (amount <= 0)
            {
                return new CurrencyTransactionResult(CurrencyTransactionStatus.InvalidAmount, Currency);
            }

            if (Currency < amount)
            {
                return new CurrencyTransactionResult(CurrencyTransactionStatus.InsufficientCurrency, Currency);
            }

            Currency -= amount;
            RaiseChanged();
            return new CurrencyTransactionResult(CurrencyTransactionStatus.Succeeded, Currency);
        }

        public void SetUpgradeLevel(UpgradeType upgradeType, int level)
        {
            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Upgrade level cannot be negative.");
            }

            if (GetUpgradeLevel(upgradeType) == level)
            {
                return;
            }

            _upgradeLevels[upgradeType] = level;
            RaiseChanged();
        }

        public void IncreaseUpgradeLevel(UpgradeType upgradeType)
        {
            SetUpgradeLevel(upgradeType, GetUpgradeLevel(upgradeType) + 1);
        }

        public void SetCompletedRaceCount(int completedRaceCount)
        {
            if (completedRaceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedRaceCount), "Completed race count cannot be negative.");
            }

            if (CompletedRaceCount == completedRaceCount)
            {
                return;
            }

            CompletedRaceCount = completedRaceCount;
            RaiseChanged();
        }

        public void RegisterCompletedRace()
        {
            SetCompletedRaceCount(CompletedRaceCount + 1);
        }

        public CurrencyTransactionResult PurchaseUpgrade(UpgradeType upgradeType, int price)
        {
            if (price < 0)
            {
                return new CurrencyTransactionResult(CurrencyTransactionStatus.InvalidAmount, Currency);
            }

            if (Currency < price)
            {
                return new CurrencyTransactionResult(CurrencyTransactionStatus.InsufficientCurrency, Currency);
            }

            Currency -= price;
            _upgradeLevels[upgradeType] = GetUpgradeLevel(upgradeType) + 1;
            RaiseChanged();
            return new CurrencyTransactionResult(CurrencyTransactionStatus.Succeeded, Currency);
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(new ProgressionStateChangedEvent(Currency, CompletedRaceCount));
        }
    }
}
