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
        private bool _setupComplete;

        public RaceSetupService(
            VehicleCatalog vehicleCatalog,
            PlayerProgressionState progressionState,
            UpgradeDefinitionData engineDefinition,
            UpgradeDefinitionData handlingDefinition,
            IRaceVehicleFactory vehicleFactory,
            IReadOnlyDictionary<string, AiDriverConfiguration> aiConfigurationsById)
        {
            _vehicleCatalog = vehicleCatalog ?? throw new ArgumentNullException(nameof(vehicleCatalog));
            _progressionState = progressionState ?? throw new ArgumentNullException(nameof(progressionState));
            _engineDefinition = engineDefinition ?? throw new ArgumentNullException(nameof(engineDefinition));
            _handlingDefinition = handlingDefinition ?? throw new ArgumentNullException(nameof(handlingDefinition));
            _vehicleFactory = vehicleFactory ?? throw new ArgumentNullException(nameof(vehicleFactory));
            _aiConfigurationsById = new Dictionary<string, AiDriverConfiguration>(aiConfigurationsById ?? new Dictionary<string, AiDriverConfiguration>(), StringComparer.Ordinal);
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

            var rosterValidation = roster.Validate(_vehicleCatalog, startingGrid != null ? startingGrid.SpawnPointCount : 0);
            if (!rosterValidation.Succeeded)
            {
                return Failure(RaceSetupStatus.InvalidRoster, rosterValidation.Message);
            }

            if (startingGrid == null)
            {
                return Failure(RaceSetupStatus.MissingSpawnPose, "Starting grid is missing.");
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

            _setupComplete = true;
            var output = new RaceSetupOutput(player, opponents, participants, player.CameraTarget, gates);
            return new RaceSetupResult(RaceSetupStatus.Succeeded, output, string.Empty);
        }

        public void Clear()
        {
            _setupComplete = false;
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
                if (!composition.CanConfigurePlayer)
                {
                    return Failure(RaceSetupStatus.MissingVehicleComposition, "The player vehicle composition is missing required references.");
                }

                var effectiveStats = VehicleUpgradeStatCalculator.Calculate(
                    vehicleLookup.Vehicle.BaseStats,
                    _progressionState,
                    _engineDefinition,
                    _handlingDefinition);
                composition.ConfigurePlayer(raceCoordinator, entry.ParticipantId, vehicleLookup.Vehicle.BaseConfiguration, effectiveStats);
                return Success(new RaceSetupOutput(composition, Array.Empty<RaceVehicleComposition>(), new[] { composition.RaceParticipant }, composition.CameraTarget, new[] { composition.DrivingInputGate }));
            }

            if (racingLine == null)
            {
                return Failure(RaceSetupStatus.MissingRacingLine, "AI race setup requires a racing line.");
            }

            if (!_aiConfigurationsById.TryGetValue(entry.AiConfigurationId, out var aiConfiguration) || aiConfiguration == null)
            {
                return Failure(RaceSetupStatus.MissingAiConfiguration, $"AI configuration '{entry.AiConfigurationId}' is missing.");
            }

            if (!composition.CanConfigureAI)
            {
                return Failure(RaceSetupStatus.MissingVehicleComposition, "The AI vehicle composition is missing required references.");
            }

            composition.ConfigureAI(raceCoordinator, entry.ParticipantId, vehicleLookup.Vehicle.BaseConfiguration, aiConfiguration, racingLine);
            return Success(new RaceSetupOutput(null, new[] { composition }, new[] { composition.RaceParticipant }, null, new[] { composition.DrivingInputGate }));
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
