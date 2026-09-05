using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.VehicleSelection.Runtime
{
    public sealed class VehicleDefinitionData
    {
        public VehicleDefinitionData(
            string stableId,
            string displayName,
            string description,
            ArcadeVehicleConfiguration baseConfiguration,
            GameObject vehiclePrefab,
            Sprite previewSprite,
            bool availableInDemo)
        {
            StableId = stableId;
            DisplayName = displayName;
            Description = description;
            BaseConfiguration = baseConfiguration;
            VehiclePrefab = vehiclePrefab;
            PreviewSprite = previewSprite;
            AvailableInDemo = availableInDemo;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public ArcadeVehicleConfiguration BaseConfiguration { get; }
        public GameObject VehiclePrefab { get; }
        public Sprite PreviewSprite { get; }
        public bool AvailableInDemo { get; }
        public ArcadeVehicleStats BaseStats => BaseConfiguration != null ? BaseConfiguration.CreateStats() : default;
        public bool HasValidId => !string.IsNullOrWhiteSpace(StableId);
    }
}
