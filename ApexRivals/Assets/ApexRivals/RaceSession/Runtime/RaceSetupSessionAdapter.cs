using System;
using ApexRivals.AI.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Configuration;
using ApexRivals.RaceSetup.Runtime;

namespace ApexRivals.RaceSession.Runtime
{
    public sealed class RaceSetupSessionAdapter : IRaceSessionSetup
    {
        private readonly RaceSetupService _raceSetupService;
        private readonly RaceSetupDefinition _raceSetupDefinition;
        private readonly SelectedVehicleState _selectedVehicleState;
        private readonly StartingGrid _startingGrid;
        private readonly RaceCoordinator _raceCoordinator;
        private readonly RacingLine _racingLine;

        private RaceSetupOutput _currentOutput;

        public RaceSetupSessionAdapter(
            RaceSetupService raceSetupService,
            RaceSetupDefinition raceSetupDefinition,
            SelectedVehicleState selectedVehicleState,
            StartingGrid startingGrid,
            RaceCoordinator raceCoordinator,
            RacingLine racingLine)
        {
            _raceSetupService = raceSetupService ?? throw new ArgumentNullException(nameof(raceSetupService));
            _raceSetupDefinition = raceSetupDefinition ?? throw new ArgumentNullException(nameof(raceSetupDefinition));
            _selectedVehicleState = selectedVehicleState ?? throw new ArgumentNullException(nameof(selectedVehicleState));
            _startingGrid = startingGrid;
            _raceCoordinator = raceCoordinator;
            _racingLine = racingLine;
        }

        public RaceSessionSetupResult Prepare()
        {
            if (_startingGrid == null)
            {
                return RaceSessionSetupResult.Failure("Starting grid is missing.");
            }

            if (_raceCoordinator == null)
            {
                return RaceSessionSetupResult.Failure("Race coordinator is missing.");
            }

            if (!_selectedVehicleState.HasCommittedVehicleSelection)
            {
                return RaceSessionSetupResult.Failure("A committed player vehicle is required before race setup.");
            }

            var roster = _raceSetupDefinition.CreateRoster(_selectedVehicleState.SelectedVehicleId);
            var setupResult = _raceSetupService.SetupRace(roster, _startingGrid, _raceCoordinator, _racingLine);
            if (!setupResult.Succeeded)
            {
                return RaceSessionSetupResult.Failure(setupResult.Message);
            }

            _currentOutput = setupResult.Output;
            return RaceSessionSetupResult.Success(setupResult.Output, roster.PlayerParticipantId);
        }

        public void Cleanup()
        {
            _raceSetupService.CleanupCurrentSetup();
            _currentOutput = null;
        }
    }
}
