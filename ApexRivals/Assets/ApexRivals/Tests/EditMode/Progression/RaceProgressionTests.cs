using System;
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
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

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

        [Test]
        public void TableOwnsAnImmutableSnapshotOfItsInput()
        {
            var input = new[] { Tier("rookie", 0, "rookie"), Tier("expert", 8, "expert") };
            var table = new RaceProgressionTable(input);
            input[0] = Tier("invalid", -1, string.Empty);
            Assert.That(table.Validate().Succeeded, Is.True);
            Assert.That(table.Tiers[0].TierId, Is.EqualTo("rookie"));
            Assert.That(table.Tiers, Is.Not.InstanceOf<RaceProgressionTier[]>());
            var exposed = table.Tiers as IList<RaceProgressionTier>;
            Assert.That(exposed, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => exposed[0] = input[0]);
            for (var index = 0; index < 100; index++)
            {
                Assert.That(new DifficultyTierResolver().Resolve(index, table).Succeeded, Is.True);
                Assert.That(table.Validate().Succeeded, Is.True);
            }
        }

        [Test]
        public void EmptyAndMissingInitialConfigurationsRetainTheirDiagnostics()
        {
            Assert.That(new RaceProgressionTable(null).Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.MissingConfiguration));
            Assert.That(new RaceProgressionTable(new[] { Tier("", 0, "rookie") }).Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.MissingConfiguration));
            Assert.That(new RaceProgressionTable(new[] { Tier("rookie", 1, "rookie") }).Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.MissingInitialTier));
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NegativeRuntimeRaceCountsRemainInvalid(int count)
        {
            var table = CreateTable();
            Assert.That(table.Validate().Succeeded, Is.True);
            Assert.That(new DifficultyTierResolver().Resolve(count, table).Succeeded, Is.False);
        }

        [Test]
        public void MissingRuntimeTableRemainsInvalid()
        {
            Assert.That(new DifficultyTierResolver().Resolve(0, null).Succeeded, Is.False);
        }

        [Test]
        public void DestroyedAiReferencesAreRecheckedAndCanBeReplaced()
        {
            var ai = CreateAiConfiguration("Rookie");
            var map = new Dictionary<string, AiDriverConfiguration> { ["rookie"] = ai };
            var table = new RaceProgressionTable(new[] { Tier("rookie", 0, "rookie") });
            var service = new RaceProgressionService(new PlayerProgressionState(), table, map);
            Assert.That(service.TryGetCurrentAiConfiguration(out _, out _), Is.True);
            UnityEngine.Object.DestroyImmediate(ai);
            Assert.That(table.Validate().Succeeded, Is.True, "The immutable table contains IDs, not Unity references.");
            Assert.That(service.Validate().Status, Is.EqualTo(RaceProgressionConfigurationStatus.MissingAiConfiguration));
            Assert.That(service.TryGetCurrentAiConfiguration(out _, out _), Is.False);
            map["rookie"] = CreateAiConfiguration("Replacement");
            Assert.That(service.TryGetCurrentAiConfiguration(out var replacement, out _), Is.True);
            Assert.That(replacement, Is.SameAs(map["rookie"]));
        }

        private RaceProgressionService CreateService(PlayerProgressionState state)
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

        private AiDriverConfiguration CreateAiConfiguration(string name)
        {
            var configuration = _objects.Own(ScriptableObject.CreateInstance<AiDriverConfiguration>());
            configuration.name = name;
            return configuration;
        }
    }
}
