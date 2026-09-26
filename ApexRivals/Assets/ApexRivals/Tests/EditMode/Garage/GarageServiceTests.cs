using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Garage
{
    public sealed class GarageServiceTests
    {
        [Test]
        public void Purchase_AffordableUpgrade_Succeeds()
        {
            var state = new PlayerProgressionState(200);
            var garage = CreateGarage(state);

            var result = garage.Purchase(UpgradeType.Engine);

            Assert.That(result.Status, Is.EqualTo(UpgradePurchaseStatus.Succeeded));
            Assert.That(state.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
        }

        [Test]
        public void Purchase_InsufficientCurrency_Fails()
        {
            var state = new PlayerProgressionState(99);
            var garage = CreateGarage(state);

            var result = garage.Purchase(UpgradeType.Engine);

            Assert.That(result.Status, Is.EqualTo(UpgradePurchaseStatus.InsufficientCurrency));
            Assert.That(state.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(state.Currency, Is.EqualTo(99));
        }

        [Test]
        public void Purchase_MaximumLevel_Fails()
        {
            var state = new PlayerProgressionState(1000);
            state.SetUpgradeLevel(UpgradeType.Engine, 2);
            var garage = CreateGarage(state);

            var result = garage.Purchase(UpgradeType.Engine);

            Assert.That(result.Status, Is.EqualTo(UpgradePurchaseStatus.MaximumLevelReached));
            Assert.That(state.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(2));
        }

        [Test]
        public void Purchase_SuccessfulPurchase_DeductsPrice()
        {
            var state = new PlayerProgressionState(200);
            var garage = CreateGarage(state);

            garage.Purchase(UpgradeType.Engine);

            Assert.That(state.Currency, Is.EqualTo(100));
        }

        [Test]
        public void CalculateEffectiveStats_EngineUpgrade_ChangesAccelerationAndSpeed()
        {
            var state = new PlayerProgressionState(200);
            var garage = CreateGarage(state);

            garage.Purchase(UpgradeType.Engine);
            var stats = garage.CalculateEffectiveStats();

            Assert.That(stats.Acceleration, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(stats.TopSpeed, Is.EqualTo(33f).Within(0.0001f));
            Assert.That(stats.Steering, Is.EqualTo(90f).Within(0.0001f));
        }

        [Test]
        public void CalculateEffectiveStats_HandlingUpgrade_ChangesSteeringAndGrip()
        {
            var state = new PlayerProgressionState(200);
            var garage = CreateGarage(state);

            garage.Purchase(UpgradeType.Handling);
            var stats = garage.CalculateEffectiveStats();

            Assert.That(stats.Acceleration, Is.EqualTo(40f).Within(0.0001f));
            Assert.That(stats.Steering, Is.EqualTo(108f).Within(0.0001f));
            Assert.That(stats.Handling, Is.EqualTo(0.85f).Within(0.0001f));
        }

        [Test]
        public void CalculateEffectiveStats_Reapplying_DoesNotAccumulateModifiers()
        {
            var state = new PlayerProgressionState(200);
            var garage = CreateGarage(state);

            garage.Purchase(UpgradeType.Engine);
            var first = garage.CalculateEffectiveStats();
            var second = garage.CalculateEffectiveStats();

            Assert.That(second.Acceleration, Is.EqualTo(first.Acceleration));
            Assert.That(second.TopSpeed, Is.EqualTo(first.TopSpeed));
        }

        [Test]
        public void GetState_BeforeAndAfterPurchase_ExposesGarageQueryState()
        {
            var state = new PlayerProgressionState(200);
            var garage = CreateGarage(state);

            var before = garage.GetState();
            garage.Purchase(UpgradeType.Engine);
            var after = garage.GetState();

            Assert.That(before.Currency, Is.EqualTo(200));
            Assert.That(before.Engine.CurrentLevel, Is.EqualTo(0));
            Assert.That(before.Engine.MaximumLevel, Is.EqualTo(2));
            Assert.That(before.Engine.NextPrice, Is.EqualTo(100));
            Assert.That(before.Engine.CanPurchase, Is.True);
            Assert.That(after.Currency, Is.EqualTo(100));
            Assert.That(after.Engine.CurrentLevel, Is.EqualTo(1));
            Assert.That(after.Engine.NextPrice, Is.EqualTo(150));
            Assert.That(after.EffectiveStats.Acceleration, Is.EqualTo(50f));
        }

        [Test]
        public void GetState_BeforeAndAfterPurchase_ExposesUpcomingBenefitModifier()
        {
            var state = new PlayerProgressionState(200);
            var garage = CreateGarage(state);

            var before = garage.GetState();
            garage.Purchase(UpgradeType.Engine);
            var after = garage.GetState();

            Assert.That(before.Engine.BenefitModifier.MaximumSpeedMultiplier, Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(after.Engine.BenefitModifier.MaximumSpeedMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
        }

        [Test]
        public void GetState_UpgradeAtMaximumLevel_ExposesCurrentBenefitModifier()
        {
            var state = new PlayerProgressionState(1000);
            state.SetUpgradeLevel(UpgradeType.Engine, 2);
            var garage = CreateGarage(state);

            var maxed = garage.GetState();

            Assert.That(maxed.Engine.IsAtMaximum, Is.True);
            Assert.That(maxed.Engine.BenefitModifier.MaximumSpeedMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
        }

        private static GarageService CreateGarage(PlayerProgressionState state)
        {
            return new GarageService(state, CreateEngineDefinition(), CreateHandlingDefinition(), CreateBaseStats());
        }

        private static UpgradeDefinitionData CreateEngineDefinition()
        {
            return new UpgradeDefinitionData(
                UpgradeType.Engine,
                new[]
                {
                    new UpgradeLevel(100, new UpgradeStatModifier(1.25f, 1.1f, 1f, 0f, 0f)),
                    new UpgradeLevel(150, new UpgradeStatModifier(1.5f, 1.2f, 1f, 0f, 0f))
                });
        }

        private static UpgradeDefinitionData CreateHandlingDefinition()
        {
            return new UpgradeDefinitionData(
                UpgradeType.Handling,
                new[]
                {
                    new UpgradeLevel(100, new UpgradeStatModifier(1f, 1f, 1.2f, 0.05f, 0.08f)),
                    new UpgradeLevel(150, new UpgradeStatModifier(1f, 1f, 1.35f, 0.1f, 0.12f))
                });
        }

        private static VehiclePerformanceStats CreateBaseStats()
        {
            return new VehiclePerformanceStats(
                40f,
                30f,
                90f,
                0.8f,
                0.4f);
        }
    }
}
