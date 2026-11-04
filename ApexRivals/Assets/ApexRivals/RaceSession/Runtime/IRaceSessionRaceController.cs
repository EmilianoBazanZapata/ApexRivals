using System;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSession.Runtime
{
    public interface IRaceSessionRaceController
    {
        event Action<RaceStartedEvent> RaceStarted;

        event Action<PositionChangedEvent> PositionChanged;

        event Action<RacerFinishedEvent> RacerFinished;

        bool ConfigureForSession(RaceSetupOutput setupOutput, string playerParticipantId, out string message);

        bool StartCountdown();

        void BlockDriving();

        void AllowDriving();

        RaceHudSnapshot CreateHudSnapshot(string playerParticipantId);

        Transform PlayerCameraTarget { get; }
        Rigidbody PlayerVehicleRigidbody { get; }
        IDrivingInputProvider PlayerInputProvider { get; }
        IVehicleTelemetry PlayerVehicleTelemetry { get; }
    }
}
