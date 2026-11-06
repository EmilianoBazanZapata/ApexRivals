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
        public void NewState_IsUncommitted()
        {
            var selectedVehicleState = new SelectedVehicleState();

            Assert.That(selectedVehicleState.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.Empty);
        }

        [Test]
        public void Select_ValidVehicle_UpdatesPreviewWithoutSaving()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, saveCheckpoint);
            var eventCount = 0;
            service.SelectionChanged += _ => eventCount++;

            var result = service.Select("striker");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.CurrentCandidateVehicleId, Is.EqualTo("striker"));
            Assert.That(selectedVehicleState.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.Empty);
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelPreview_DoesNotCommitSelection()
        {
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, new FakeSelectionSaveCheckpoint());

            service.Select("striker");
            service.CancelPreview();

            Assert.That(service.CurrentCandidateVehicleId, Is.EqualTo("vanguard"));
            Assert.That(selectedVehicleState.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.Empty);
        }

        [Test]
        public void Select_UnknownVehicle_IsRejectedWithoutSaving()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, saveCheckpoint);

            var result = service.Select("missing");

            Assert.That(result.Status, Is.EqualTo(VehicleSelectionStatus.UnknownVehicle));
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.Empty);
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void Select_UnavailableVehicle_IsRejectedWithoutSaving()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, saveCheckpoint);

            var result = service.Select("locked");

            Assert.That(result.Status, Is.EqualTo(VehicleSelectionStatus.UnavailableVehicle));
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.Empty);
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void Select_AlreadySelectedVehicle_DoesNotEmitDuplicateEvent()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, saveCheckpoint);
            var eventCount = 0;
            service.SelectionChanged += _ => eventCount++;

            var result = service.Select("vanguard");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
            Assert.That(eventCount, Is.EqualTo(0));
        }

        [Test]
        public void ConfirmSelection_ValidVehicle_CommitsOnceAndSavesOnce()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, saveCheckpoint);
            var committedCount = 0;
            service.VehicleSelectionCommitted += _ => committedCount++;

            service.Select("striker");
            var result = service.ConfirmSelection();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(selectedVehicleState.HasCommittedVehicleSelection, Is.True);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("striker"));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
            Assert.That(committedCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmSelection_SaveFailure_RollsBackCommitment()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint { ShouldFail = true };
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, saveCheckpoint);

            service.Select("striker");
            var result = service.ConfirmSelection();

            Assert.That(result.Status, Is.EqualTo(VehicleSelectionStatus.SaveFailed));
            Assert.That(selectedVehicleState.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.Empty);
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmSelection_ReconfirmingSameVehicle_IsIdempotent()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState();
            var service = CreateService(selectedVehicleState, saveCheckpoint);
            var committedCount = 0;
            service.VehicleSelectionCommitted += _ => committedCount++;

            service.Select("striker");
            service.ConfirmSelection();
            var result = service.ConfirmSelection();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("striker"));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
            Assert.That(committedCount, Is.EqualTo(1));
        }

        [Test]
        public void Select_DifferentVehicleAfterCommitment_UpdatesCandidateWithoutSaving()
        {
            var saveCheckpoint = new FakeSelectionSaveCheckpoint();
            var selectedVehicleState = new SelectedVehicleState("vanguard", true);
            var service = CreateService(selectedVehicleState, saveCheckpoint);

            var result = service.Select("striker");

            Assert.That(result.Status, Is.EqualTo(VehicleSelectionStatus.Succeeded));
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.EqualTo("vanguard"));
            Assert.That(service.CurrentCandidateVehicleId, Is.EqualTo("striker"));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void GetState_ExposesBaseAndEffectiveStats()
        {
            var progression = new PlayerProgressionState();
            progression.SetUpgradeLevel(UpgradeType.Engine, 1);
            progression.SetUpgradeLevel(UpgradeType.Handling, 1);
            var selectedVehicleState = new SelectedVehicleState("vanguard", true);
            var service = CreateService(selectedVehicleState, new FakeSelectionSaveCheckpoint(), progression);

            var state = service.GetState();

            Assert.That(state.SelectedVehicleId, Is.EqualTo("vanguard"));
            Assert.That(state.SelectedVehicle.EffectivePlayerStats.Acceleration, Is.GreaterThan(state.SelectedVehicle.BaseStats.Acceleration));
            Assert.That(state.SelectedVehicle.EffectivePlayerStats.Steering, Is.GreaterThan(state.SelectedVehicle.BaseStats.Steering));
        }

        [Test]
        public void EnsurePersistedSelectionIsValid_InvalidCommittedId_ClearsCommitment()
        {
            var selectedVehicleState = new SelectedVehicleState("missing", true);
            var service = CreateService(selectedVehicleState, new FakeSelectionSaveCheckpoint());

            var result = service.EnsurePersistedSelectionIsValid();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(selectedVehicleState.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicleState.SelectedVehicleId, Is.Empty);
            Assert.That(service.CurrentCandidateVehicleId, Is.EqualTo("vanguard"));
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
                CreateVehicle("starter", true, false),
                CreateVehicle("vanguard", true),
                CreateVehicle("striker", true),
                CreateVehicle("locked", false)
            }, "starter");
        }

        private static VehicleDefinitionData CreateVehicle(string id, bool available, bool hasPrefab = true)
        {
            var configuration = ScriptableObject.CreateInstance<WheelVehicleConfiguration>();
            configuration.performanceAcceleration = 10f;
            configuration.performanceTopSpeed = 20f;
            configuration.performanceSteering = 30f;
            return new VehicleDefinitionData(id, id, string.Empty, configuration, hasPrefab ? new GameObject($"{id}Prefab") : null, null, available);
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
