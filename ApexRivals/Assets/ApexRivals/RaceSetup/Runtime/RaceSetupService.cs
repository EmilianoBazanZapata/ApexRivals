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

            // Reuse track geometry as read-only data; driver behaviour is independent of ranking.
            var positionRoute = new List<UnityEngine.Vector3>();
            if (racingLine != null && racingLine.IsValid)
                for (var i = 0; i < racingLine.WaypointCount; i++) positionRoute.Add(racingLine.GetWaypointPosition(i));
            raceCoordinator.ConfigurePositionRoute(positionRoute);

            var spawned = new List<RaceVehicleComposition>();
            var opponents = new List<RaceVehicleComposition>();
            var participants = new List<RaceParticipant>();
            var gates = new List<DrivingInputGate>();
            RaceVehicleComposition player = null;

            for (var index = 0; index < roster.Entries.Count; index++)
            {
                var entry = roster.Entries[index];
                var status = SetupEntry(entry, startingGrid, raceCoordinator, racingLine, spawned, out var composition, out var message);
                if (status != RaceSetupStatus.Succeeded)
                {
                    Cleanup(spawned);
                    return Failure(status, message);
                }

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

        private RaceSetupStatus SetupEntry(
            RaceRosterEntry entry,
            StartingGrid startingGrid,
            RaceCoordinator raceCoordinator,
            RacingLine racingLine,
            List<RaceVehicleComposition> spawned,
            out RaceVehicleComposition composition,
            out string message)
        {
            composition = null;
            message = string.Empty;
            if (!startingGrid.TryGetSpawnPose(entry.StartingGridIndex, out var spawnPose))
            {
                message = $"No spawn pose exists for grid index '{entry.StartingGridIndex}'.";
                return RaceSetupStatus.MissingSpawnPose;
            }

            var vehicleLookup = _vehicleCatalog.FindById(entry.VehicleId);
            if (!vehicleLookup.Succeeded)
            {
                message = vehicleLookup.Message;
                return RaceSetupStatus.MissingVehicleDefinition;
            }

            if (vehicleLookup.Vehicle.VehiclePrefab == null)
            {
                message = $"Vehicle '{entry.VehicleId}' has no prefab assigned.";
                return RaceSetupStatus.MissingVehiclePrefab;
            }

            if (!_vehicleFactory.TryCreate(vehicleLookup.Vehicle.VehiclePrefab, spawnPose, out composition, out message))
            {
                return RaceSetupStatus.SpawnFailed;
            }

            spawned.Add(composition);

            if (entry.IsPlayer)
            {
                if (!composition.TryValidatePlayer(out message))
                {
                    return RaceSetupStatus.MissingVehicleComposition;
                }

                var effectiveStats = VehicleUpgradeStatCalculator.Calculate(
                    vehicleLookup.Vehicle.BasePerformanceStats,
                    _progressionState,
                    _engineDefinition,
                    _handlingDefinition);
                composition.ConfigureValidatedPlayer(raceCoordinator, entry.ParticipantId, effectiveStats);
                return RaceSetupStatus.Succeeded;
            }

            if (racingLine == null)
            {
                message = "AI race setup requires a racing line.";
                return RaceSetupStatus.MissingRacingLine;
            }

            if (!TryResolveAiConfiguration(entry.AiConfigurationId, out var aiConfiguration, out message))
            {
                return _raceDifficultyProvider != null ? RaceSetupStatus.DifficultyResolutionFailed : RaceSetupStatus.MissingAiConfiguration;
            }

            if (!composition.TryValidateAI(out message))
            {
                return RaceSetupStatus.MissingVehicleComposition;
            }

            composition.ConfigureValidatedAI(raceCoordinator, entry.ParticipantId, aiConfiguration, racingLine);
            return RaceSetupStatus.Succeeded;
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

        private static RaceSetupResult Failure(RaceSetupStatus status, string message)
        {
            return new RaceSetupResult(status, null, message);
        }
    }
}
