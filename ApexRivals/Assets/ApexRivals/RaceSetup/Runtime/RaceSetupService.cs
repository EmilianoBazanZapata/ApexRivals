using System;
using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.AI.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Input.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.RaceSetup.Runtime
{
    public sealed class RaceSetupService
    {
        private readonly VehicleCatalog _vehicleCatalog;
        private readonly PlayerProgressionState _progressionState;
        private readonly UpgradeDefinitionData _engineDefinition;
        private readonly UpgradeDefinitionData _handlingDefinition;
        private readonly IRaceVehicleFactory _vehicleFactory;
        private readonly Dictionary<string, AiDriverConfiguration> _aiConfigurationsById;
        private readonly IRaceDifficultyProvider _raceDifficultyProvider;
        private readonly List<RaceVehicleComposition> _currentSpawned = new List<RaceVehicleComposition>();
        private bool _setupComplete;

        public RaceSetupService(
            VehicleCatalog vehicleCatalog,
            PlayerProgressionState progressionState,
            UpgradeDefinitionData engineDefinition,
            UpgradeDefinitionData handlingDefinition,
            IRaceVehicleFactory vehicleFactory,
            IReadOnlyDictionary<string, AiDriverConfiguration> aiConfigurationsById,
            IRaceDifficultyProvider raceDifficultyProvider = null)
        {
            _vehicleCatalog = vehicleCatalog ?? throw new ArgumentNullException(nameof(vehicleCatalog));
            _progressionState = progressionState ?? throw new ArgumentNullException(nameof(progressionState));
            _engineDefinition = engineDefinition ?? throw new ArgumentNullException(nameof(engineDefinition));
            _handlingDefinition = handlingDefinition ?? throw new ArgumentNullException(nameof(handlingDefinition));
            _vehicleFactory = vehicleFactory ?? throw new ArgumentNullException(nameof(vehicleFactory));
            _aiConfigurationsById = new Dictionary<string, AiDriverConfiguration>(aiConfigurationsById ?? new Dictionary<string, AiDriverConfiguration>(), StringComparer.Ordinal);
            _raceDifficultyProvider = raceDifficultyProvider;
        }

        public RaceSetupResult SetupRace(
            RaceRoster roster,
            StartingGrid startingGrid,
            RaceCoordinator raceCoordinator,
            RacingLine racingLine)
        {
            if (_setupComplete)
            {
                return Failure(RaceSetupStatus.AlreadySetup, "Race setup has already completed.");
            }

            if (roster == null)
            {
                return Failure(RaceSetupStatus.InvalidRoster, "Race roster is missing.");
            }

            if (startingGrid == null)
            {
                return Failure(RaceSetupStatus.MissingSpawnPose, "Starting grid is missing.");
            }

            var startingGridValidation = startingGrid.Validate();
            if (!startingGridValidation.Succeeded)
            {
                return Failure(RaceSetupStatus.InvalidStartingGrid, startingGridValidation.Message);
            }

            var rosterValidation = roster.Validate(_vehicleCatalog, startingGrid.SpawnPointCount);
            if (!rosterValidation.Succeeded)
            {
                return Failure(RaceSetupStatus.InvalidRoster, rosterValidation.Message);
            }

            if (raceCoordinator == null)
            {
                return Failure(RaceSetupStatus.InvalidRoster, "Race coordinator is missing.");
            }

            var spawned = new List<RaceVehicleComposition>();
            var opponents = new List<RaceVehicleComposition>();
            var participants = new List<RaceParticipant>();
            var gates = new List<DrivingInputGate>();
            RaceVehicleComposition player = null;

            for (var index = 0; index < roster.Entries.Count; index++)
            {
                var entry = roster.Entries[index];
                var setupEntryResult = SetupEntry(entry, startingGrid, raceCoordinator, racingLine, spawned);
                if (!setupEntryResult.Succeeded)
                {
                    Cleanup(spawned);
                    return setupEntryResult;
                }

                var composition = setupEntryResult.Output.Player ?? setupEntryResult.Output.Opponents[0];
                participants.Add(composition.RaceParticipant);
                gates.Add(composition.DrivingInputGate);

                if (entry.IsPlayer)
                {
                    player = composition;
                }
                else
                {
                    opponents.Add(composition);
                }
            }

            _currentSpawned.Clear();
            _currentSpawned.AddRange(spawned);
            _setupComplete = true;
            var output = new RaceSetupOutput(player, opponents, participants, player.CameraTarget, gates);
            return new RaceSetupResult(RaceSetupStatus.Succeeded, output, string.Empty);
        }

        public void Clear()
        {
            _setupComplete = false;
        }

        public void CleanupCurrentSetup()
        {
            Cleanup(_currentSpawned);
            _currentSpawned.Clear();
            Clear();
        }

        private RaceSetupResult SetupEntry(
            RaceRosterEntry entry,
            StartingGrid startingGrid,
            RaceCoordinator raceCoordinator,
            RacingLine racingLine,
            List<RaceVehicleComposition> spawned)
        {
            if (!startingGrid.TryGetSpawnPose(entry.StartingGridIndex, out var spawnPose))
            {
                return Failure(RaceSetupStatus.MissingSpawnPose, $"No spawn pose exists for grid index '{entry.StartingGridIndex}'.");
            }

            var vehicleLookup = _vehicleCatalog.FindById(entry.VehicleId);
            if (!vehicleLookup.Succeeded)
            {
                return Failure(RaceSetupStatus.MissingVehicleDefinition, vehicleLookup.Message);
            }

            if (vehicleLookup.Vehicle.VehiclePrefab == null)
            {
                return Failure(RaceSetupStatus.MissingVehiclePrefab, $"Vehicle '{entry.VehicleId}' has no prefab assigned.");
            }

            if (!_vehicleFactory.TryCreate(vehicleLookup.Vehicle.VehiclePrefab, spawnPose, out var composition, out var message))
            {
                return Failure(RaceSetupStatus.SpawnFailed, message);
            }

            spawned.Add(composition);

            if (entry.IsPlayer)
            {
                if (!composition.TryValidatePlayer(out var playerValidationMessage))
                {
                    return Failure(RaceSetupStatus.MissingVehicleComposition, playerValidationMessage);
                }

                var effectiveStats = VehicleUpgradeStatCalculator.Calculate(
                    vehicleLookup.Vehicle.BasePerformanceStats,
                    _progressionState,
                    _engineDefinition,
                    _handlingDefinition);
                composition.ConfigurePlayer(raceCoordinator, entry.ParticipantId, effectiveStats);
                return Success(new RaceSetupOutput(composition, Array.Empty<RaceVehicleComposition>(), new[] { composition.RaceParticipant }, composition.CameraTarget, new[] { composition.DrivingInputGate }));
            }

            if (racingLine == null)
            {
                return Failure(RaceSetupStatus.MissingRacingLine, "AI race setup requires a racing line.");
            }

            if (!TryResolveAiConfiguration(entry.AiConfigurationId, out var aiConfiguration, out var aiMessage))
            {
                return Failure(
                    _raceDifficultyProvider != null ? RaceSetupStatus.DifficultyResolutionFailed : RaceSetupStatus.MissingAiConfiguration,
                    aiMessage);
            }

            if (!composition.TryValidateAI(out var aiValidationMessage))
            {
                return Failure(RaceSetupStatus.MissingVehicleComposition, aiValidationMessage);
            }

            composition.ConfigureAI(raceCoordinator, entry.ParticipantId, aiConfiguration, racingLine);
            return Success(new RaceSetupOutput(null, new[] { composition }, new[] { composition.RaceParticipant }, null, new[] { composition.DrivingInputGate }));
        }

        private bool TryResolveAiConfiguration(string aiConfigurationId, out AiDriverConfiguration aiConfiguration, out string message)
        {
            if (_raceDifficultyProvider != null)
            {
                return _raceDifficultyProvider.TryGetCurrentAiConfiguration(out aiConfiguration, out message);
            }

            if (_aiConfigurationsById.TryGetValue(aiConfigurationId, out aiConfiguration) && aiConfiguration != null)
            {
                message = string.Empty;
                return true;
            }

            message = $"AI configuration '{aiConfigurationId}' is missing.";
            return false;
        }

        private void Cleanup(IReadOnlyList<RaceVehicleComposition> spawned)
        {
            for (var index = spawned.Count - 1; index >= 0; index--)
            {
                _vehicleFactory.Destroy(spawned[index]);
            }
        }

        private static RaceSetupResult Success(RaceSetupOutput output)
        {
            return new RaceSetupResult(RaceSetupStatus.Succeeded, output, string.Empty);
        }

        private static RaceSetupResult Failure(RaceSetupStatus status, string message)
        {
            return new RaceSetupResult(status, null, message);
        }
    }
}
