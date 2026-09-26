using System;
using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Runtime;

namespace ApexRivals.Garage.Runtime
{
    public sealed class GarageService
    {
        private readonly PlayerProgressionState _progressionState;
        private readonly UpgradeDefinitionData _engineDefinition;
        private readonly UpgradeDefinitionData _handlingDefinition;
        private readonly Func<VehiclePerformanceStats> _baseStatsProvider;

        public GarageService(
            PlayerProgressionState progressionState,
            UpgradeDefinitionData engineDefinition,
            UpgradeDefinitionData handlingDefinition,
            VehiclePerformanceStats baseStats)
            : this(progressionState, engineDefinition, handlingDefinition, () => baseStats)
        {
        }

        public GarageService(
            PlayerProgressionState progressionState,
            UpgradeDefinitionData engineDefinition,
            UpgradeDefinitionData handlingDefinition,
            Func<VehiclePerformanceStats> baseStatsProvider)
        {
            _progressionState = progressionState ?? throw new ArgumentNullException(nameof(progressionState));
            _engineDefinition = engineDefinition ?? throw new ArgumentNullException(nameof(engineDefinition));
            _handlingDefinition = handlingDefinition ?? throw new ArgumentNullException(nameof(handlingDefinition));
            _baseStatsProvider = baseStatsProvider ?? throw new ArgumentNullException(nameof(baseStatsProvider));
        }

        public GarageState GetState()
        {
            return new GarageState(
                _progressionState.Currency,
                CreateUpgradeState(_engineDefinition),
                CreateUpgradeState(_handlingDefinition),
                CalculateEffectiveStats());
        }

        public UpgradePurchaseResult Purchase(UpgradeType upgradeType)
        {
            var definition = GetDefinition(upgradeType);
            if (definition == null)
            {
                return CreateResult(UpgradePurchaseStatus.UnknownUpgrade, upgradeType, 0, 0);
            }

            var currentLevel = _progressionState.GetUpgradeLevel(upgradeType);
            if (currentLevel < 0 || currentLevel > definition.MaximumLevel)
            {
                return CreateResult(UpgradePurchaseStatus.InvalidConfiguration, upgradeType, currentLevel, 0);
            }

            if (!definition.HasNextLevel(currentLevel))
            {
                return CreateResult(UpgradePurchaseStatus.MaximumLevelReached, upgradeType, currentLevel, 0);
            }

            var price = definition.GetNextPrice(currentLevel);
            if (!_progressionState.CanAfford(price))
            {
                return CreateResult(UpgradePurchaseStatus.InsufficientCurrency, upgradeType, currentLevel, price);
            }

            var purchaseResult = _progressionState.PurchaseUpgrade(upgradeType, price);
            if (!purchaseResult.Succeeded)
            {
                if (purchaseResult.Status == CurrencyTransactionStatus.InvalidAmount)
                {
                    return CreateResult(UpgradePurchaseStatus.InvalidConfiguration, upgradeType, currentLevel, price);
                }

                return CreateResult(UpgradePurchaseStatus.InsufficientCurrency, upgradeType, currentLevel, price);
            }

            return CreateResult(UpgradePurchaseStatus.Succeeded, upgradeType, currentLevel + 1, price);
        }

        public VehiclePerformanceStats CalculateEffectiveStats()
        {
            return VehicleUpgradeStatCalculator.Calculate(_baseStatsProvider(), _progressionState, _engineDefinition, _handlingDefinition);
        }

        private GarageUpgradeState CreateUpgradeState(UpgradeDefinitionData definition)
        {
            var level = _progressionState.GetUpgradeLevel(definition.UpgradeType);
            var nextPrice = definition.GetNextPrice(level);
            var benefitLevel = definition.HasNextLevel(level) ? level + 1 : level;
            return new GarageUpgradeState(
                definition.UpgradeType,
                level,
                definition.MaximumLevel,
                nextPrice,
                definition.HasNextLevel(level) && _progressionState.CanAfford(nextPrice),
                definition.GetModifierForLevel(benefitLevel));
        }

        private UpgradeDefinitionData GetDefinition(UpgradeType upgradeType)
        {
            return upgradeType switch
            {
                UpgradeType.Engine => _engineDefinition,
                UpgradeType.Handling => _handlingDefinition,
                _ => null
            };
        }

        private UpgradePurchaseResult CreateResult(UpgradePurchaseStatus status, UpgradeType upgradeType, int level, int price)
        {
            return new UpgradePurchaseResult(status, upgradeType, level, price, _progressionState.Currency, CalculateEffectiveStats());
        }
    }
}
