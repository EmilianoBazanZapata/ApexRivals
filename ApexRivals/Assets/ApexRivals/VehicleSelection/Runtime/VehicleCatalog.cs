using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexRivals.VehicleSelection.Runtime
{
    public sealed class VehicleCatalog
    {
        private readonly List<VehicleDefinitionData> _vehicles;
        private readonly Dictionary<string, VehicleDefinitionData> _vehiclesById;

        public VehicleCatalog(IEnumerable<VehicleDefinitionData> vehicles, string defaultVehicleId)
        {
            _vehicles = new List<VehicleDefinitionData>(vehicles ?? Array.Empty<VehicleDefinitionData>());
            DefaultVehicleId = defaultVehicleId;
            _vehiclesById = new Dictionary<string, VehicleDefinitionData>(StringComparer.Ordinal);

            for (var index = 0; index < _vehicles.Count; index++)
            {
                var vehicle = _vehicles[index];
                if (vehicle != null && vehicle.HasValidId && !_vehiclesById.ContainsKey(vehicle.StableId))
                {
                    _vehiclesById.Add(vehicle.StableId, vehicle);
                }
            }
        }

        public string DefaultVehicleId { get; }
        public IReadOnlyList<VehicleDefinitionData> Vehicles => _vehicles;

        public VehicleCatalogValidationResult Validate()
        {
            if (_vehicles.Count == 0)
            {
                return new VehicleCatalogValidationResult(VehicleCatalogStatus.MissingDefinition, "The vehicle catalog contains no vehicle definitions.");
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _vehicles.Count; index++)
            {
                var vehicle = _vehicles[index];
                if (vehicle == null)
                {
                    return new VehicleCatalogValidationResult(VehicleCatalogStatus.MissingDefinition, "The vehicle catalog contains a missing vehicle definition.");
                }

                if (!vehicle.HasValidId)
                {
                    return new VehicleCatalogValidationResult(VehicleCatalogStatus.MissingVehicleId, "A vehicle definition has an empty stable ID.");
                }

                if (!seenIds.Add(vehicle.StableId))
                {
                    return new VehicleCatalogValidationResult(VehicleCatalogStatus.DuplicateVehicleId, $"Duplicate vehicle ID '{vehicle.StableId}' found.");
                }
            }

            var defaultLookup = FindById(DefaultVehicleId);
            if (!defaultLookup.Succeeded || !defaultLookup.Vehicle.AvailableInDemo)
            {
                return new VehicleCatalogValidationResult(VehicleCatalogStatus.InvalidDefaultVehicleId, "The default vehicle ID must reference an available demo vehicle.");
            }

            return new VehicleCatalogValidationResult(VehicleCatalogStatus.Succeeded, string.Empty);
        }

        public VehicleLookupResult FindById(string vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return new VehicleLookupResult(VehicleCatalogStatus.UnknownVehicleId, null, "The vehicle ID is empty.");
            }

            return _vehiclesById.TryGetValue(vehicleId, out var vehicle)
                ? new VehicleLookupResult(VehicleCatalogStatus.Succeeded, vehicle, string.Empty)
                : new VehicleLookupResult(VehicleCatalogStatus.UnknownVehicleId, null, $"Vehicle ID '{vehicleId}' is not in the catalog.");
        }

        public IReadOnlyList<VehicleDefinitionData> GetSelectableDemoVehicles()
        {
            return _vehicles.Where(vehicle => vehicle != null && vehicle.HasValidId && vehicle.AvailableInDemo).ToArray();
        }

        public VehicleDefinitionData ResolveSavedOrDefault(string savedVehicleId)
        {
            var savedLookup = FindById(savedVehicleId);
            if (savedLookup.Succeeded && savedLookup.Vehicle.AvailableInDemo)
            {
                return savedLookup.Vehicle;
            }

            var defaultLookup = FindById(DefaultVehicleId);
            return defaultLookup.Succeeded ? defaultLookup.Vehicle : null;
        }
    }
}
