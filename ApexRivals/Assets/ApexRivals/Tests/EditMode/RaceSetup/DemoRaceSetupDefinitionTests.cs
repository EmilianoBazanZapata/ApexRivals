using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Configuration;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.RaceSetup
{
    public sealed class DemoRaceSetupDefinitionTests
    {
        private const string AssetPath = "Assets/ApexRivals/Content/Configuration/Race/DemoRaceSetupDefinition.asset";

        [Test]
        public void CreateRoster_SelectedPlayerVehicleAndSingleAuthoredOpponentUseSlotsZeroAndOne()
        {
            var definition = AssetDatabase.LoadAssetAtPath<RaceSetupDefinition>(AssetPath);

            Assert.That(definition, Is.Not.Null);
            var roster = definition.CreateRoster("vanguard");

            Assert.That(roster.Entries.Count, Is.EqualTo(2));
            Assert.That(roster.Entries[0].EntryType, Is.EqualTo(RaceEntryType.Player));
            Assert.That(roster.Entries[0].VehicleId, Is.EqualTo("vanguard"));
            Assert.That(roster.Entries[0].StartingGridIndex, Is.EqualTo(0));

            var opponent = roster.Entries[1];
            Assert.That(opponent.EntryType, Is.EqualTo(RaceEntryType.AI));
            Assert.That(opponent.ParticipantId, Is.EqualTo("AI_01"));
            Assert.That(opponent.StartingGridIndex, Is.EqualTo(1));
            Assert.That(opponent.VehicleId, Is.Not.Empty);
            Assert.That(opponent.AiConfigurationId, Is.EqualTo("standard"));
        }

        [TestCase("Assets/ApexRivals/Vehicle/Prefabs/Vehicle_Vanguard.prefab")]
        [TestCase("Assets/ApexRivals/Vehicle/Prefabs/Vehicle_Striker.prefab")]
        public void ProductionOpponentPrefab_HasValidSharedVehicleAndAiComposition(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(prefab.GetComponent<WheelArcadeVehicleController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<VehicleResetter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<RaceParticipant>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<DrivingInputGate>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ApexRivals.AI.Runtime.AiDrivingInputProvider>(), Is.Not.Null);

            var composition = prefab.GetComponent<RaceVehicleComposition>();
            Assert.That(composition, Is.Not.Null);
            Assert.That(composition.TryValidateAI(out var message), Is.True, message);
        }
    }
}
