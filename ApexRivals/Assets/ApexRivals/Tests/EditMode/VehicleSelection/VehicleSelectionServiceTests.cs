using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.VehicleSelection
{
    public sealed class VehicleSelectionServiceTests
    {
        [Test]
        public void Select_ValidVehicle_ChangesSelectionAndSaves()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState("starter");
            var service = CreateService(selectedVehicleState, saveCheckpoint);
            var eventCount = 0;
            service.SelectionChanged += _ => eventCount++;

            var result = service.Select("sport");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("sport"));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void Select_UnknownVehicle_IsRejectedWithoutSaving()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState("starter");
            var service = CreateService(selectedVehicleState, saveCheckpoint);

            var result = service.Select("missing");

            Assert.That(result.Status, Is.EqualTo(VehicleSelectionStatus.UnknownVehicle));
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("starter"));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void Select_UnavailableVehicle_IsRejectedWithoutSaving()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState("starter");
            var service = CreateService(selectedVehicleState, saveCheckpoint);

            var result = service.Select("locked");

            Assert.That(result.Status, Is.EqualTo(VehicleSelectionStatus.UnavailableVehicle));
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("starter"));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void Select_AlreadySelectedVehicle_DoesNotEmitDuplicateEvent()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState("starter");
            var service = CreateService(selectedVehicleState, saveCheckpoint);
            var eventCount = 0;
            service.SelectionChanged += _ => eventCount++;

            var result = service.Select("starter");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
            Assert.That(eventCount, Is.EqualTo(0));
        }

        [Test]
        public void Select_SaveFailure_RollsBackSelection()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint { ShouldFail = true };
            var selectedVehicleState = new SelectedVehicleState("starter");
            var service = CreateService(selectedVehicleState, saveCheckpoint);
            var eventCount = 0;
            service.SelectionChanged += _ => eventCount++;

            var result = service.Select("sport");

            Assert.That(result.Status, Is.EqualTo(VehicleSelectionStatus.SaveFailed));
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("starter"));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
            Assert.That(eventCount, Is.EqualTo(0));
        }

        [Test]
        public void GetState_ExposesBaseAndEffectiveStats()
        {
            var progression = new PlayerProgressionState();
            progression.SetUpgradeLevel(UpgradeType.Engine, 1);
            progression.SetUpgradeLevel(UpgradeType.Handling, 1);
            var selectedVehicleState = new SelectedVehicleState("starter");
            var service = CreateService(selectedVehicleState, new FakeSelectionSaveCheckpoint(), progression);

            var state = service.GetState();

            Assert.That(state.SelectedVehicleId, Is.EqualTo("starter"));
            Assert.That(state.SelectedVehicle.EffectivePlayerStats.ForwardAcceleration, Is.GreaterThan(state.SelectedVehicle.BaseStats.ForwardAcceleration));
            Assert.That(state.SelectedVehicle.EffectivePlayerStats.SteeringStrength, Is.GreaterThan(state.SelectedVehicle.BaseStats.SteeringStrength));
        }

        [Test]
        public void EnsurePersistedSelectionIsValid_InvalidSavedId_FallsBackToDefault()
        {
            var selectedVehicleState = new SelectedVehicleState("missing");
            var service = CreateService(selectedVehicleState, new FakeSelectionSaveCheckpoint());

            var result = service.EnsurePersistedSelectionIsValid();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("starter"));
        }

        private static VehicleSelectionService CreateService(
            SelectedVehicleState selectedVehicleState,
            FakeSelectionSaveCheckpoint saveCheckpoint,
            PlayerProgressionState progression = null)
        {
            return new VehicleSelectionService(
                CreateCatalog(),
                selectedVehicleState,
                saveCheckpoint,
                progression,
                CreateEngineDefinition(),
                CreateHandlingDefinition());
        }

        private static VehicleCatalog CreateCatalog()
        {
            return new VehicleCatalog(new[]
            {
                CreateVehicle("starter", true),
                CreateVehicle("sport", true),
                CreateVehicle("locked", false)
            }, "starter");
        }

        private static VehicleDefinitionData CreateVehicle(string id, bool available)
        {
            var configuration = ScriptableObject.CreateInstance<ArcadeVehicleConfiguration>();
            configuration.forwardAcceleration = 10f;
            configuration.maximumForwardSpeed = 20f;
            configuration.steeringStrength = 30f;
            return new VehicleDefinitionData(id, id, string.Empty, configuration, new GameObject($"{id}Prefab"), null, available);
        }

        private static UpgradeDefinitionData CreateEngineDefinition()
        {
            return new UpgradeDefinitionData(
                UpgradeType.Engine,
                new[] { new UpgradeLevel(100, new UpgradeStatModifier(1.5f, 1.2f, 1f, 0f, 0f)) });
        }

        private static UpgradeDefinitionData CreateHandlingDefinition()
        {
            return new UpgradeDefinitionData(
                UpgradeType.Handling,
                new[] { new UpgradeLevel(100, new UpgradeStatModifier(1f, 1f, 1.4f, 0.05f, 0f)) });
        }

        private sealed class FakeSelectionSaveCheckpoint : IVehicleSelectionSaveCheckpoint
        {
            public int SaveCount { get; private set; }
            public bool ShouldFail { get; set; }

            public VehicleSelectionSaveResult Save()
            {
                SaveCount++;
                return ShouldFail ? VehicleSelectionSaveResult.Failure("Save failed.") : VehicleSelectionSaveResult.Success();
            }
        }
    }
}
