using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Progression
{
    public sealed class RaceProgressionTests
    {
        [Test]
        public void NewProfileStartsWithZeroCompletedRaces()
        {
            var state = new PlayerProgressionState();

            Assert.That(state.CompletedRaceCount, Is.Zero);
        }

        [TestCase(0, "rookie")]
        [TestCase(1, "rookie")]
        [TestCase(2, "amateur")]
        [TestCase(4, "amateur")]
        [TestCase(5, "professional")]
        [TestCase(7, "professional")]
        [TestCase(8, "expert")]
        [TestCase(99, "expert")]
        public void TierResolution_SelectsExpectedTier(int completedRaceCount, string expectedTier)
        {
            var result = new DifficultyTierResolver().Resolve(completedRaceCount, CreateTable());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Tier.TierId, Is.EqualTo(expectedTier));
        }

        [Test]
        public void CompletingPlayerRaceIncrementsOnceAndReportsTierUp()
        {
            var state = new PlayerProgressionState(completedRaceCount: 1);
            var service = CreateService(state);
            var tierChangedCount = 0;
            service.DifficultyTierChanged += _ => tierChangedCount++;

            var result = service.RegisterCompletedPlayerRace();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.CompletedRaceCount, Is.EqualTo(2));
            Assert.That(result.PreviousTier.TierId, Is.EqualTo("rookie"));
            Assert.That(result.CurrentTier.TierId, Is.EqualTo("amateur"));
            Assert.That(result.NewTierReached, Is.True);
            Assert.That(tierChangedCount, Is.EqualTo(1));
        }

        [Test]
        public void CompletingWithoutTierChangeDoesNotFireDifficultyEvent()
        {
            var state = new PlayerProgressionState(completedRaceCount: 2);
            var service = CreateService(state);
            var tierChangedCount = 0;
            service.DifficultyTierChanged += _ => tierChangedCount++;

            var result = service.RegisterCompletedPlayerRace();

            Assert.That(result.NewTierReached, Is.False);
            Assert.That(tierChangedCount, Is.Zero);
        }

        [Test]
        public void UpgradesAndCurrencyDoNotAffectDifficulty()
        {
            var state = new PlayerProgressionState(completedRaceCount: 1);
            var service = CreateService(state);
            var before = service.ResolveCurrentTier();

            state.AddCurrency(1000);
            state.SetUpgradeLevel(UpgradeType.Engine, 2);
            state.SetUpgradeLevel(UpgradeType.Handling, 2);
            var after = service.ResolveCurrentTier();

            Assert.That(before.Tier.TierId, Is.EqualTo("rookie"));
            Assert.That(after.Tier.TierId, Is.EqualTo("rookie"));
        }

        [Test]
        public void InvalidConfigurationRulesAreRejected()
        {
            Assert.That(new RaceProgressionTable(new[] { Tier("rookie", 0, "rookie"), Tier("rookie", 2, "amateur") }).Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.DuplicateTierId));
            Assert.That(new RaceProgressionTable(new[] { Tier("rookie", -1, "rookie") }).Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.NegativeThreshold));
            Assert.That(new RaceProgressionTable(new[] { Tier("rookie", 0, "rookie"), Tier("amateur", 0, "amateur") }).Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.UnorderedThreshold));
            Assert.That(new RaceProgressionTable(new[] { Tier("rookie", 0, string.Empty) }).Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.MissingAiConfiguration));
        }

        [Test]
        public void MissingAiConfigurationReferenceIsRejected()
        {
            var state = new PlayerProgressionState();
            var service = new RaceProgressionService(
                state,
                CreateTable(),
                new Dictionary<string, AiDriverConfiguration> { ["rookie"] = CreateAiConfiguration("Rookie") });

            var validation = service.Validate();

            Assert.That(validation.Status, Is.EqualTo(RaceProgressionConfigurationStatus.MissingAiConfiguration));
        }

        private static RaceProgressionService CreateService(PlayerProgressionState state)
        {
            return new RaceProgressionService(
                state,
                CreateTable(),
                new Dictionary<string, AiDriverConfiguration>
                {
                    ["rookie"] = CreateAiConfiguration("Rookie"),
                    ["amateur"] = CreateAiConfiguration("Amateur"),
                    ["professional"] = CreateAiConfiguration("Professional"),
                    ["expert"] = CreateAiConfiguration("Expert")
                });
        }

        private static RaceProgressionTable CreateTable()
        {
            return new RaceProgressionTable(new[]
            {
                Tier("rookie", 0, "rookie"),
                Tier("amateur", 2, "amateur"),
                Tier("professional", 5, "professional"),
                Tier("expert", 8, "expert")
            });
        }

        private static RaceProgressionTier Tier(string id, int threshold, string aiConfigurationId)
        {
            return new RaceProgressionTier(id, id, threshold, aiConfigurationId);
        }

        private static AiDriverConfiguration CreateAiConfiguration(string name)
        {
            var configuration = ScriptableObject.CreateInstance<AiDriverConfiguration>();
            configuration.name = name;
            return configuration;
        }
    }
}
