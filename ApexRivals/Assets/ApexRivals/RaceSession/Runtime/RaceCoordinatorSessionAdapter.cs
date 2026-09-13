using System;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSession.Runtime
{
    public sealed class RaceCoordinatorSessionAdapter : IRaceSessionRaceController
    {
        private readonly RaceCoordinator _raceCoordinator;
        private RaceSetupOutput _setupOutput;
        private string _playerParticipantId = string.Empty;

        public RaceCoordinatorSessionAdapter(RaceCoordinator raceCoordinator)
        {
            _raceCoordinator = raceCoordinator ?? throw new ArgumentNullException(nameof(raceCoordinator));
        }

        public event Action<RaceStartedEvent> RaceStarted
        {
            add => _raceCoordinator.RaceStarted += value;
            remove => _raceCoordinator.RaceStarted -= value;
        }

        public event Action<RacerFinishedEvent> RacerFinished
        {
            add => _raceCoordinator.RacerFinished += value;
            remove => _raceCoordinator.RacerFinished -= value;
        }

        public Transform PlayerCameraTarget => _setupOutput != null ? _setupOutput.PlayerCameraTarget : null;
        public Rigidbody PlayerVehicleRigidbody => _setupOutput != null && _setupOutput.Player != null
            ? _setupOutput.Player.VehicleRigidbody
            : null;
        public IDrivingInputProvider PlayerInputProvider => _setupOutput != null && _setupOutput.Player != null
            ? _setupOutput.Player.DrivingInputGate
            : null;
        public IVehicleTelemetry PlayerVehicleTelemetry => _setupOutput != null && _setupOutput.Player != null
            ? _setupOutput.Player.VehicleTelemetry
            : null;

        public bool ConfigureForSession(RaceSetupOutput setupOutput, string playerParticipantId, out string message)
        {
            _setupOutput = setupOutput;
            _playerParticipantId = playerParticipantId;

            if (setupOutput == null)
            {
                message = "Race setup output is missing.";
                return false;
            }

            if (setupOutput.Participants == null || setupOutput.Participants.Count == 0)
            {
                message = "Race setup output has no participants.";
                return false;
            }

            if (setupOutput.DrivingGates == null || setupOutput.DrivingGates.Count == 0)
            {
                message = "Race setup output has no driving gates.";
                return false;
            }

            var playerParticipant = setupOutput.Player != null ? setupOutput.Player.RaceParticipant : null;
            if (playerParticipant == null)
            {
                message = "Player race participant is missing.";
                return false;
            }

            _raceCoordinator.ConfigureSession(setupOutput.Participants, setupOutput.DrivingGates, playerParticipant);
            message = string.Empty;
            return true;
        }

        public bool StartCountdown()
        {
            return _raceCoordinator.StartCountdown();
        }

        public void BlockDriving()
        {
            _raceCoordinator.SetSessionDrivingAllowed(false);
        }

        public void AllowDriving()
        {
            _raceCoordinator.SetSessionDrivingAllowed(true);
        }

        public RaceHudSnapshot CreateHudSnapshot(string playerParticipantId)
        {
            var currentLap = 0;
            if (_setupOutput != null && _setupOutput.Player != null && _raceCoordinator.TryGetProgress(_setupOutput.Player.RaceParticipant, out var progress))
            {
                currentLap = progress.CurrentLap;
            }

            return new RaceHudSnapshot(
                currentLap,
                _raceCoordinator.TotalLaps,
                string.IsNullOrWhiteSpace(_playerParticipantId) ? 0 : _raceCoordinator.GetLivePosition(_playerParticipantId),
                _setupOutput != null && _setupOutput.Participants != null ? _setupOutput.Participants.Count : 0,
                _raceCoordinator.CountdownRemaining,
                _raceCoordinator.ElapsedRaceTime);
        }
    }
}
