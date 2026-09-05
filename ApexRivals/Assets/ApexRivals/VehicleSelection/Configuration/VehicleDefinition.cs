using ApexRivals.Vehicle.Configuration;
using ApexRivals.VehicleSelection.Runtime;
using UnityEngine;

namespace ApexRivals.VehicleSelection.Configuration
{
    [CreateAssetMenu(fileName = "VehicleDefinition", menuName = "Apex Rivals/Vehicle Selection/Vehicle Definition")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [SerializeField] private string stableId = "starter";
        [SerializeField] private string displayName = "Starter";
        [SerializeField] private string description;
        [SerializeField] private ArcadeVehicleConfiguration baseConfiguration;
        [SerializeField] private GameObject vehiclePrefab;
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private bool availableInDemo = true;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public bool AvailableInDemo => availableInDemo;

        public VehicleDefinitionData CreateData()
        {
            return new VehicleDefinitionData(
                stableId,
                displayName,
                description,
                baseConfiguration,
                vehiclePrefab,
                previewSprite,
                availableInDemo);
        }
    }
}
