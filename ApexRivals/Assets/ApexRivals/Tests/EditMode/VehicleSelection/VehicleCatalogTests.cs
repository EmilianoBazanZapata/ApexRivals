using ApexRivals.Vehicle.Configuration;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.VehicleSelection
{
    public sealed class VehicleCatalogTests
    {
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

        [Test]
        public void FindById_KnownVehicle_ReturnsDefinition()
        {
            var catalog = CreateCatalog();

            var result = catalog.FindById("sport");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Vehicle.DisplayName, Is.EqualTo("Sport"));
        }

        [Test]
        public void FindById_UnknownVehicle_ReturnsFailure()
        {
            var catalog = CreateCatalog();

            var result = catalog.FindById("missing");

            Assert.That(result.Status, Is.EqualTo(VehicleCatalogStatus.UnknownVehicleId));
        }

        [Test]
        public void Validate_DuplicateIds_ReturnsFailure()
        {
            var catalog = new VehicleCatalog(new[]
            {
                CreateVehicle("starter", true),
                CreateVehicle("starter", true)
            }, "starter");

            var result = catalog.Validate();

            Assert.That(result.Status, Is.EqualTo(VehicleCatalogStatus.DuplicateVehicleId));
        }

        [Test]
        public void Validate_InvalidDefaultId_ReturnsFailure()
        {
            var catalog = new VehicleCatalog(new[] { CreateVehicle("starter", true) }, "missing");

            var result = catalog.Validate();

            Assert.That(result.Status, Is.EqualTo(VehicleCatalogStatus.InvalidDefaultVehicleId));
        }

        [Test]
        public void ResolveSavedOrDefault_InvalidSavedId_ReturnsDefault()
        {
            var catalog = CreateCatalog();

            var vehicle = catalog.ResolveSavedOrDefault("missing");

            Assert.That(vehicle.StableId, Is.EqualTo("starter"));
        }

        [Test]
        public void GetRaceSelectableVehicles_UsesProductionEligibilityWithoutHidingCatalogEntries()
        {
            var catalog = CreateCatalog();

            var vehicles = catalog.GetRaceSelectableVehicles();

            Assert.That(vehicles.Count, Is.EqualTo(1));
            Assert.That(vehicles[0].StableId, Is.EqualTo("sport"));
            Assert.That(catalog.Vehicles.Count, Is.EqualTo(3));
            Assert.That(catalog.FindById("locked").Succeeded, Is.True);
        }

        [Test]
        public void StarterWithoutPrefabRemainsAValidCatalogFallbackButCannotRace()
        {
            var starter = CreateVehicle("starter", true, false);
            var catalog = new VehicleCatalog(new[] { starter }, "starter");

            Assert.That(catalog.Validate().Succeeded, Is.True);
            Assert.That(catalog.ResolveSavedOrDefault("missing"), Is.SameAs(starter));
            Assert.That(catalog.GetRaceSelectableVehicles(), Is.Empty);
            Assert.That(VehicleCatalog.IsRaceSelectable(starter), Is.False);
            Assert.That(VehicleCatalog.IsRaceSelectable(null), Is.False);
        }

        private VehicleCatalog CreateCatalog()
        {
            return new VehicleCatalog(new[]
            {
                CreateVehicle("starter", true),
                CreateVehicle("sport", true),
                CreateVehicle("locked", false)
            }, "starter");
        }

        private VehicleDefinitionData CreateVehicle(string id, bool available, bool hasPrefab = true)
        {
            var configuration = _objects.Own(ScriptableObject.CreateInstance<WheelVehicleConfiguration>());
            return new VehicleDefinitionData(id, id == "sport" ? "Sport" : id, string.Empty, configuration, hasPrefab ? _objects.Own(new GameObject($"{id}Prefab")) : null, null, available);
        }
    }
}
