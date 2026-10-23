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
                vehicleSelectionCommitted = vehicleSelectionCommitted,
                career = NormalizeCareer(saveData.career)
            };

            return new SaveDataValidationResult(true, validated, string.Empty);
        }

        /// <summary>
        /// Trims and de-duplicates career IDs and drops blanks. IDs the current content does not know are kept:
        /// the career ignores them, and a later build that knows them again still finds the progress.
        /// </summary>
        private static CareerSaveData NormalizeCareer(CareerSaveData career)
        {
            if (career == null)
            {
                return new CareerSaveData();
            }

            return new CareerSaveData
            {
                ownedVehicleIds = NormalizeIds(career.ownedVehicleIds),
                completedTrackIds = NormalizeIds(career.completedTrackIds),
                wonTrackIds = NormalizeIds(career.wonTrackIds),
                achievedMilestoneIds = NormalizeIds(career.achievedMilestoneIds),
                finaleWon = career.finaleWon
            };
        }

        private static string[] NormalizeIds(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return Array.Empty<string>();
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var normalized = new List<string>(ids.Length);
            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id) && seen.Add(id.Trim()))
                {
                    normalized.Add(id.Trim());
                }
            }

            return normalized.ToArray();
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
