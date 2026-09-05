using System;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSession.Runtime
{
    public interface IRaceSessionRaceController
    {
        event Action<RaceStartedEvent> RaceStarted;

        event Action<RacerFinishedEvent> RacerFinished;

        bool ConfigureForSession(RaceSetupOutput setupOutput, string playerParticipantId, out string message);

        bool StartCountdown();

        void BlockDriving();

        RaceHudSnapshot CreateHudSnapshot(string playerParticipantId);

        Transform PlayerCameraTarget { get; }
    }
}
