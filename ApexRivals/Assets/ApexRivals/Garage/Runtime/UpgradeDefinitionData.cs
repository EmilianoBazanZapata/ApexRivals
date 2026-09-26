using System;
using System.Collections.Generic;
using ApexRivals.Progression.Runtime;

namespace ApexRivals.Garage.Runtime
{
    public sealed class UpgradeDefinitionData
    {
        private readonly UpgradeLevel[] _levels;

        public UpgradeDefinitionData(UpgradeType upgradeType, IReadOnlyList<UpgradeLevel> levels)
        {
            UpgradeType = upgradeType;
            _levels = new UpgradeLevel[levels?.Count ?? 0];

            for (var index = 0; index < _levels.Length; index++)
            {
                _levels[index] = levels[index];
            }
        }

        public UpgradeType UpgradeType { get; }
        public int MaximumLevel => _levels.Length;

        public bool HasNextLevel(int currentLevel)
        {
            return currentLevel >= 0 && currentLevel < MaximumLevel;
        }

        public int GetNextPrice(int currentLevel)
        {
            if (!HasNextLevel(currentLevel))
            {
                return 0;
            }

            return _levels[currentLevel].Price;
        }

        public UpgradeStatModifier GetModifierForLevel(int level)
        {
            if (level < 0 || level > MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Upgrade level is outside the configured range.");
            }

            return level == 0 ? UpgradeStatModifier.Identity : _levels[level - 1].StatModifier;
        }
    }
}
