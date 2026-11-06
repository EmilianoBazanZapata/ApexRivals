using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.RaceSetup
{
    public sealed class RaceRosterTests
    {
        [Test]
        public void Validate_OnePlayerAndValidAiEntries_Succeeds()
        {
            var roster = CreateRoster(
                Player("Player", "starter", 0),
                AI("AI_01", "starter", 1, "standard"));

            var result = roster.Validate(CreateCatalog(), 2);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_NoPlayer_ReturnsFailure()
        {
            var roster = CreateRoster(AI("AI_01", "starter", 0, "standard"));

            var result = roster.Validate(CreateCatalog(), 1);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.MissingPlayer));
        }

        [Test]
        public void Validate_MultiplePlayers_ReturnsFailure()
        {
            var roster = CreateRoster(Player("Player", "starter", 0), Player("Player2", "starter", 1));

            var result = roster.Validate(CreateCatalog(), 2);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.MultiplePlayers));
        }

        [Test]
        public void Validate_DuplicateParticipantIds_ReturnsFailure()
        {
            var roster = CreateRoster(Player("Player", "starter", 0), AI("Player", "starter", 1, "standard"));

            var result = roster.Validate(CreateCatalog(), 2);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.DuplicateParticipantId));
        }

        [Test]
        public void Validate_DuplicateGridIndices_ReturnsFailure()
        {
            var roster = CreateRoster(Player("Player", "starter", 0), AI("AI_01", "starter", 0, "standard"));

            var result = roster.Validate(CreateCatalog(), 2);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.DuplicateGridIndex));
        }

        [Test]
        public void Validate_NegativeGridIndex_ReturnsFailure()
        {
            var roster = CreateRoster(Player("Player", "starter", -1));

            var result = roster.Validate(CreateCatalog(), 1);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.NegativeGridIndex));
        }

        [Test]
        public void Validate_UnknownVehicleId_ReturnsFailure()
        {
            var roster = CreateRoster(Player("Player", "missing", 0));

            var result = roster.Validate(CreateCatalog(), 1);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.UnknownVehicleId));
        }

        [Test]
        public void Validate_MissingAiConfiguration_ReturnsFailure()
        {
            var roster = CreateRoster(Player("Player", "starter", 0), AI("AI_01", "starter", 1, string.Empty));

            var result = roster.Validate(CreateCatalog(), 2);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.MissingAiConfiguration));
        }

        [Test]
        public void Validate_InsufficientSpawnPoints_ReturnsFailure()
        {
            var roster = CreateRoster(Player("Player", "starter", 0), AI("AI_01", "starter", 1, "standard"));

            var result = roster.Validate(CreateCatalog(), 1);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.InsufficientSpawnPoints));
            Assert.That(result.Message, Is.EqualTo("Race requires 2 grid slots for 1 player + 1 AI opponents, but StartingGrid contains 1."));
        }

        [Test]
        public void Validate_OnePlayerAndThreeAiWithThreeGridSlots_FailsWithRequiredCapacity()
        {
            var roster = CreateRoster(
                Player("Player", "starter", 0),
                AI("AI_01", "starter", 1, "standard"),
                AI("AI_02", "starter", 2, "standard"),
                AI("AI_03", "starter", 3, "standard"));

            var result = roster.Validate(CreateCatalog(), 3);

            Assert.That(result.Status, Is.EqualTo(RaceRosterValidationStatus.InsufficientSpawnPoints));
            Assert.That(result.Message, Is.EqualTo("Race requires 4 grid slots for 1 player + 3 AI opponents, but StartingGrid contains 3."));
        }

        private static RaceRoster CreateRoster(params RaceRosterEntry[] entries)
        {
            return new RaceRoster(entries);
        }

        private static RaceRosterEntry Player(string participantId, string vehicleId, int gridIndex)
        {
            return new RaceRosterEntry(participantId, RaceEntryType.Player, vehicleId, gridIndex, string.Empty);
        }

        private static RaceRosterEntry AI(string participantId, string vehicleId, int gridIndex, string aiConfigurationId)
        {
            return new RaceRosterEntry(participantId, RaceEntryType.AI, vehicleId, gridIndex, aiConfigurationId);
        }

        private static VehicleCatalog CreateCatalog()
        {
            var configuration = ScriptableObject.CreateInstance<WheelVehicleConfiguration>();
            var prefab = new GameObject("VehiclePrefab");
            return new VehicleCatalog(new[]
            {
                new VehicleDefinitionData("starter", "Starter", string.Empty, configuration, prefab, null, true)
            }, "starter");
        }
    }
}
