using System;
using System.Collections.Generic;
using ApexRivals.Garage.Runtime;

namespace ApexRivals.SaveSystem.Runtime
{
    public sealed class PlayerProfileSaveValidator
    {
        private readonly HashSet<string> _knownVehicleIds;

        public PlayerProfileSaveValidator(string fallbackVehicleId = SelectedVehicleState.DefaultVehicleId, IEnumerable<string> knownVehicleIds = null)
        {
            var normalizedFallbackVehicleId = string.IsNullOrWhiteSpace(fallbackVehicleId)
                ? SelectedVehicleState.DefaultVehicleId
                : fallbackVehicleId.Trim();
            _knownVehicleIds = knownVehicleIds != null
                ? new HashSet<string>(knownVehicleIds, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            if (_knownVehicleIds.Count > 0)
            {
                _knownVehicleIds.Add(normalizedFallbackVehicleId);
            }
        }

        public SaveDataValidationResult Validate(PlayerProfileSaveData saveData)
        {
            if (saveData == null)
            {
                return new SaveDataValidationResult(false, null, "Save data is missing.");
            }

            if (saveData.schemaVersion != PlayerProfileSaveData.CurrentSchemaVersion)
            {
                return new SaveDataValidationResult(false, null, "Save schema version is unsupported.");
            }

            if (saveData.currency < 0)
            {
                return new SaveDataValidationResult(false, null, "Currency cannot be negative.");
            }

            if (saveData.engineUpgradeLevel < 0 || saveData.handlingUpgradeLevel < 0)
            {
                return new SaveDataValidationResult(false, null, "Upgrade levels cannot be negative.");
            }

            if (saveData.completedRaceCount < 0)
            {
                return new SaveDataValidationResult(false, null, "Completed race count cannot be negative.");
            }

            var selectedVehicleId = ResolveVehicleId(saveData.selectedVehicleId, saveData.vehicleSelectionCommitted, out var vehicleSelectionCommitted);
            var validated = new PlayerProfileSaveData
            {
                schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                currency = saveData.currency,
                engineUpgradeLevel = saveData.engineUpgradeLevel,
                handlingUpgradeLevel = saveData.handlingUpgradeLevel,
                completedRaceCount = saveData.completedRaceCount,
                selectedVehicleId = selectedVehicleId,
                vehicleSelectionCommitted = vehicleSelectionCommitted
            };

            return new SaveDataValidationResult(true, validated, string.Empty);
        }

        private string ResolveVehicleId(string vehicleId, bool vehicleSelectionCommitted, out bool resolvedCommitted)
        {
            resolvedCommitted = false;
            if (!vehicleSelectionCommitted || string.IsNullOrWhiteSpace(vehicleId))
            {
                return string.Empty;
            }

            var normalizedVehicleId = vehicleId.Trim();
            if (_knownVehicleIds.Count > 0 && !_knownVehicleIds.Contains(normalizedVehicleId))
            {
                return string.Empty;
            }

            resolvedCommitted = true;
            return normalizedVehicleId;
        }
    }
}
