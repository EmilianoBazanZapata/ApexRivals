using System;
using System.Collections.Generic;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.RaceSetup.Runtime
{
    public sealed class RaceRoster
    {
        private readonly List<RaceRosterEntry> _entries;

        public RaceRoster(IEnumerable<RaceRosterEntry> entries)
        {
            _entries = new List<RaceRosterEntry>(entries ?? Array.Empty<RaceRosterEntry>());
        }

        public IReadOnlyList<RaceRosterEntry> Entries => _entries;
        public string PlayerParticipantId
        {
            get
            {
                for (var index = 0; index < _entries.Count; index++)
                {
                    if (_entries[index].IsPlayer)
                    {
                        return _entries[index].ParticipantId;
                    }
                }

                return string.Empty;
            }
        }

        public RaceRosterValidationResult Validate(VehicleCatalog vehicleCatalog, int? availableSpawnPointCount = null)
        {
            if (vehicleCatalog == null)
            {
                throw new ArgumentNullException(nameof(vehicleCatalog));
            }

            var playerCount = 0;
            var participantIds = new HashSet<string>(StringComparer.Ordinal);
            var gridIndices = new HashSet<int>();

            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];
                if (string.IsNullOrWhiteSpace(entry.ParticipantId))
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.MissingParticipantId, "A race entry is missing a participant ID.");
                }

                if (!participantIds.Add(entry.ParticipantId))
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.DuplicateParticipantId, $"Duplicate participant ID '{entry.ParticipantId}' found.");
                }

                if (entry.StartingGridIndex < 0)
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.NegativeGridIndex, "Starting grid indices cannot be negative.");
                }

                if (!gridIndices.Add(entry.StartingGridIndex))
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.DuplicateGridIndex, $"Duplicate starting grid index '{entry.StartingGridIndex}' found.");
                }

                var vehicleLookup = vehicleCatalog.FindById(entry.VehicleId);
                if (!vehicleLookup.Succeeded)
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.UnknownVehicleId, vehicleLookup.Message);
                }

                if (entry.IsPlayer)
                {
                    playerCount++;
                }
                else if (string.IsNullOrWhiteSpace(entry.AiConfigurationId))
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.MissingAiConfiguration, "AI race entries require an AI configuration ID.");
                }
            }

            if (playerCount == 0)
            {
                return new RaceRosterValidationResult(RaceRosterValidationStatus.MissingPlayer, "The race roster must contain one player entry.");
            }

            if (playerCount > 1)
            {
                return new RaceRosterValidationResult(RaceRosterValidationStatus.MultiplePlayers, "The race roster cannot contain more than one player entry.");
            }

            if (availableSpawnPointCount.HasValue && _entries.Count > availableSpawnPointCount.Value)
            {
                var opponentCount = _entries.Count - playerCount;
                return new RaceRosterValidationResult(
                    RaceRosterValidationStatus.InsufficientSpawnPoints,
                    $"Race requires {_entries.Count} grid slots for {playerCount} player + {opponentCount} AI opponents, but StartingGrid contains {availableSpawnPointCount.Value}.");
            }

            return new RaceRosterValidationResult(RaceRosterValidationStatus.Succeeded, string.Empty);
        }
    }
}
