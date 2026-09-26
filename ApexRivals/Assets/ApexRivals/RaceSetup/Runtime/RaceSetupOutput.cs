using System.Collections.Generic;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    public sealed class RaceSetupOutput
    {
        public RaceSetupOutput(
            RaceVehicleComposition player,
            IReadOnlyList<RaceVehicleComposition> opponents,
            IReadOnlyList<RaceParticipant> participants,
            Transform playerCameraTarget,
            IReadOnlyList<DrivingInputGate> drivingGates)
        {
            Player = player;
            Opponents = opponents;
            Participants = participants;
            PlayerCameraTarget = playerCameraTarget;
            DrivingGates = drivingGates;
        }

        public RaceVehicleComposition Player { get; }
        public IReadOnlyList<RaceVehicleComposition> Opponents { get; }
        public IReadOnlyList<RaceParticipant> Participants { get; }
        public Transform PlayerCameraTarget { get; }
        public IReadOnlyList<DrivingInputGate> DrivingGates { get; }
    }
}
