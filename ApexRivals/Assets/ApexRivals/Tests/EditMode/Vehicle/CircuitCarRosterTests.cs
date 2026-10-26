using System.Linq;
using ApexRivals.Career.Configuration;
using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.Tracks.Configuration;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VehicleSelection.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class CircuitCarRosterTests
    {
        private const string Content = "Assets/ApexRivals/Content/Configuration/";
        private static readonly string[] Ids = { "vanguard", "striker", "sunaru", "borshe911", "aduo", "fernedenz", "massalini", "fehhahi" };

        [TestCase("vanguard")]
        [TestCase("striker")]
        [TestCase("sunaru")]
        [TestCase("borshe911")]
        [TestCase("aduo")]
        [TestCase("fernedenz")]
        [TestCase("massalini")]
        [TestCase("fehhahi")]
        public void EveryCatalogCarHasItsOwnPlayableRigAndConfiguration(string id)
        {
            var catalog = Catalog();
            Assert.That(catalog.Vehicles.Select(v => v.StableId), Is.EquivalentTo(Ids));
            var lookup = catalog.FindById(id);
            Assert.That(lookup.Succeeded, Is.True);
            var prefab = lookup.Vehicle.VehiclePrefab;
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(prefab.GetComponentsInChildren<WheelCollider>().Length, Is.EqualTo(4));
            var controller = prefab.GetComponent<WheelArcadeVehicleController>();
            var data = new SerializedObject(controller);
            var config = data.FindProperty("configuration").objectReferenceValue as WheelVehicleConfiguration;
            Assert.That(config, Is.Not.Null);
            Assert.That(config.wheelRadius, Is.InRange(0.2f, 0.4f));
            Assert.That(data.FindProperty("useRuntimeTuningOverrides").boolValue, Is.False);
            Assert.That(AssetDatabase.GetAssetPath(prefab.transform.Find("VisualRoot/Body").GetComponent<MeshFilter>().sharedMesh), Does.EndWith(id + "_Body.asset"));
            foreach (var suffix in new[] { "FL", "FR", "RL", "RR" })
            {
                var wheel = prefab.transform.Find("WheelCollider_" + suffix).GetComponent<WheelCollider>();
                Assert.That(wheel.radius, Is.EqualTo(config.wheelRadius).Within(0.001f));
                Assert.That(prefab.transform.Find("VisualRoot/WheelVisualRoot_" + suffix).localScale, Is.EqualTo(Vector3.one));
            }
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
        }

        [Test]
        public void FreshCareerOwnsExactlyOneCarAndAllOthersHavePaidOffers()
        {
            var catalog = Catalog();
            var definition = AssetDatabase.LoadAssetAtPath<CareerDefinition>(Content + "Career/CareerDefinition.asset");
            var tracks = AssetDatabase.LoadAssetAtPath<TrackCatalogDefinition>("Assets/ApexRivals/Content/Tracks/TrackCatalogDefinition.asset").CreateCatalog();
            var state = new CareerState();
            var career = new CareerService(definition.CreateConfiguration(), state, new PlayerProgressionState(),
                new SelectedVehicleState("vanguard", true), catalog, tracks);
            career.Initialize();
            Assert.That(state.OwnedVehicleIds, Is.EqualTo(new[] { "vanguard" }));
            Assert.That(catalog.DefaultVehicleId, Is.EqualTo("vanguard"));
            foreach (var id in Ids.Where(id => id != "vanguard"))
            {
                var status = career.GetVehicleStatus(id);
                Assert.That(status.Owned, Is.False);
                Assert.That(status.ForSale, Is.True);
                Assert.That(status.Price, Is.GreaterThan(0));
                Assert.That(status.PrerequisiteMet, Is.False);
            }
        }

        [Test]
        public void NewCarPurchasesAndLegacyOwnershipSurviveSaveAndReload()
        {
            var catalog = Catalog();
            var definition = AssetDatabase.LoadAssetAtPath<CareerDefinition>(Content + "Career/CareerDefinition.asset");
            var tracks = AssetDatabase.LoadAssetAtPath<TrackCatalogDefinition>("Assets/ApexRivals/Content/Tracks/TrackCatalogDefinition.asset").CreateCatalog();
            var progression = new PlayerProgressionState(); progression.AddCurrency(50000);
            var selected = new SelectedVehicleState("striker", true);
            var state = new CareerState(); state.GrantVehicle("striker");
            foreach (var milestone in new[] { "apex-win", "dunes-win", "crossover-win" }) state.MarkMilestoneAchieved(milestone);
            var storage = new MemoryStorage();
            var save = new PlayerProfileSaveService(progression, selected, storage, Ids, state);
            var career = new CareerService(definition.CreateConfiguration(), state, progression, selected, catalog, tracks);
            career.Initialize();
            foreach (var id in Ids.Where(id => id != "vanguard" && id != "striker"))
                Assert.That(career.PurchaseVehicle(id).Succeeded, Is.True, id);
            selected.Commit("fehhahi");
            Assert.That(save.Save().Succeeded, Is.True);
            var reloaded = new CareerState();
            var loadedSelection = new SelectedVehicleState();
            var loader = new PlayerProfileSaveService(new PlayerProgressionState(), loadedSelection, storage, Ids, reloaded);
            Assert.That(loader.LoadOrCreateDefault().Succeeded, Is.True);
            Assert.That(reloaded.OwnedVehicleIds, Is.EquivalentTo(Ids));
            Assert.That(loadedSelection.SelectedVehicleId, Is.EqualTo("fehhahi"));
            Assert.That(reloaded.OwnsVehicle("striker"), Is.True);
        }

        private static ApexRivals.VehicleSelection.Runtime.VehicleCatalog Catalog() =>
            AssetDatabase.LoadAssetAtPath<VehicleCatalogDefinition>(Content + "Vehicles/VehicleCatalogDefinition.asset").CreateCatalog();

        private sealed class MemoryStorage : IPlayerProfileSaveStorage
        {
            private string _json;
            public bool SaveExists() => _json != null;
            public bool BackupExists() => false;
            public SaveStorageReadResult ReadSave() => new SaveStorageReadResult(true, _json, string.Empty);
            public SaveStorageReadResult ReadBackup() => new SaveStorageReadResult(false, null, "No backup");
            public SaveStorageWriteResult WriteSave(string json, bool preserveExistingBackup = false) { _json = json; return new SaveStorageWriteResult(true, string.Empty); }
            public SaveStorageWriteResult DeleteSave() { _json = null; return new SaveStorageWriteResult(true, string.Empty); }
        }
    }
}
