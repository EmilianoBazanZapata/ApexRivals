using ApexRivals.Vehicle.Configuration;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.VehicleSelection
{
    public sealed class VehicleCatalogTests
    {
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
        public void GetSelectableDemoVehicles_OnlyReturnsAvailableVehicles()
        {
            var catalog = CreateCatalog();

            var vehicles = catalog.GetSelectableDemoVehicles();

            Assert.That(vehicles.Count, Is.EqualTo(2));
            Assert.That(vehicles[0].StableId, Is.EqualTo("starter"));
            Assert.That(vehicles[1].StableId, Is.EqualTo("sport"));
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
            var configuration = ScriptableObject.CreateInstance<WheelVehicleConfiguration>();
            return new VehicleDefinitionData(id, id == "sport" ? "Sport" : id, string.Empty, configuration, new GameObject($"{id}Prefab"), null, available);
        }
    }
}
