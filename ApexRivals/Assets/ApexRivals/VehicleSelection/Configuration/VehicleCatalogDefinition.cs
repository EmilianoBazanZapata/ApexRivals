using ApexRivals.VehicleSelection.Runtime;
using UnityEngine;

namespace ApexRivals.VehicleSelection.Configuration
{
    [CreateAssetMenu(fileName = "VehicleCatalogDefinition", menuName = "Apex Rivals/Vehicle Selection/Vehicle Catalog")]
    public sealed class VehicleCatalogDefinition : ScriptableObject
    {
        [SerializeField] private string defaultVehicleId = "starter";
        [SerializeField] private VehicleDefinition[] vehicles = System.Array.Empty<VehicleDefinition>();

        public string DefaultVehicleId => defaultVehicleId;

        public VehicleCatalog CreateCatalog()
        {
            var vehicleData = new VehicleDefinitionData[vehicles.Length];
            for (var index = 0; index < vehicles.Length; index++)
            {
                vehicleData[index] = vehicles[index] != null ? vehicles[index].CreateData() : null;
            }

            return new VehicleCatalog(vehicleData, defaultVehicleId);
        }
    }
}
