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
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

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

        private VehicleSelectionService CreateService(
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

        [TestCase("vanguard", true, true, true)]
        [TestCase("starter", true, true, false)]
        [TestCase("locked", false, true, false)]
        [TestCase("no-prefab", true, false, false)]
        [TestCase(" ", true, true, false)]
        public void RaceEligibilityPreservesProductionRequirements(string id, bool available, bool hasPrefab, bool expected)
        {
            var vehicle = CreateVehicle(id, available, hasPrefab);
            var catalog = new VehicleCatalog(new[] { CreateVehicle("starter", true, false), vehicle }, "starter");
            var selection = new SelectedVehicleState();
            var save = new FakeSelectionSaveCheckpoint();
            var service = new VehicleSelectionService(catalog, selection, save);

            Assert.That(service.AvailableVehicles.Count, Is.EqualTo(expected ? 1 : 0));
            Assert.That(service.GetState().Vehicles.Count, Is.EqualTo(expected ? 1 : 0));
            var result = service.Select(id);
            Assert.That(result.Succeeded, Is.EqualTo(expected));
            Assert.That(selection.HasCommittedVehicleSelection, Is.False);
            Assert.That(save.SaveCount, Is.Zero);
        }

        [Test]
        public void RaceEligibilityKeepsAuthoredOrderAndRechecksDestroyedPrefabs()
        {
            var striker = CreateVehicle("striker", true);
            var vanguard = CreateVehicle("vanguard", true);
            var catalog = new VehicleCatalog(new[] { CreateVehicle("starter", true, false), striker, CreateVehicle("locked", false), vanguard }, "starter");
            var selection = new SelectedVehicleState("vanguard", true);
            var service = new VehicleSelectionService(catalog, selection);
            Assert.That(service.AvailableVehicles[0], Is.SameAs(striker));
            Assert.That(service.AvailableVehicles[1], Is.SameAs(vanguard));

            Object.DestroyImmediate(vanguard.VehiclePrefab);
            var result = service.EnsurePersistedSelectionIsValid();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(selection.HasCommittedVehicleSelection, Is.False);
            Assert.That(service.CurrentCandidateVehicleId, Is.EqualTo("striker"));
            Assert.That(service.AvailableVehicles.Count, Is.EqualTo(1));
            Assert.That(service.GetState().Vehicles[0].VehicleId, Is.EqualTo("striker"));
        }

        private VehicleCatalog CreateCatalog()
        {
            return new VehicleCatalog(new[]
            {
                CreateVehicle("starter", true, false),
                CreateVehicle("vanguard", true),
                CreateVehicle("striker", true),
                CreateVehicle("locked", false)
            }, "starter");
        }

        private VehicleDefinitionData CreateVehicle(string id, bool available, bool hasPrefab = true)
        {
            var configuration = _objects.Own(ScriptableObject.CreateInstance<WheelVehicleConfiguration>());
            configuration.performanceAcceleration = 10f;
            configuration.performanceTopSpeed = 20f;
            configuration.performanceSteering = 30f;
            return new VehicleDefinitionData(id, id, string.Empty, configuration, hasPrefab ? _objects.Own(new GameObject($"{id}Prefab")) : null, null, available);
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
