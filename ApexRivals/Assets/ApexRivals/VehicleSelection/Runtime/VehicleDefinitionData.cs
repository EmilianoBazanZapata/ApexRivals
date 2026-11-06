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
            ScriptableObject baseConfiguration,
            GameObject vehiclePrefab,
            Sprite previewSprite,
            bool availableInDemo)
            : this(
                stableId,
                displayName,
                description,
                baseConfiguration,
                baseConfiguration as IVehiclePerformanceStatsSource,
                vehiclePrefab,
                previewSprite,
                availableInDemo)
        {
        }

        public VehicleDefinitionData(
            string stableId,
            string displayName,
            string description,
            ScriptableObject baseConfiguration,
            IVehiclePerformanceStatsSource performanceStatsSource,
            GameObject vehiclePrefab,
            Sprite previewSprite,
            bool availableInDemo)
        {
            StableId = stableId;
            DisplayName = displayName;
            Description = description;
            BaseConfiguration = baseConfiguration;
            _performanceStatsSource = performanceStatsSource;
            VehiclePrefab = vehiclePrefab;
            PreviewSprite = previewSprite;
            AvailableInDemo = availableInDemo;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        private readonly IVehiclePerformanceStatsSource _performanceStatsSource;

        public ScriptableObject BaseConfiguration { get; }
        public GameObject VehiclePrefab { get; }
        public Sprite PreviewSprite { get; }
        public bool AvailableInDemo { get; }
        public VehiclePerformanceStats BasePerformanceStats => _performanceStatsSource != null
            ? _performanceStatsSource.CreatePerformanceStats()
            : default;
        public bool HasValidId => !string.IsNullOrWhiteSpace(StableId);
    }
}
