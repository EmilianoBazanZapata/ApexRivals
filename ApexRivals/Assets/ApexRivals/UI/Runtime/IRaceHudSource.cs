using System;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;

namespace ApexRivals.UI.Runtime
{
    public interface IRaceHudSource
    {
        event Action<CountdownChangedEvent> CountdownChanged;
        event Action<LapCompletedEvent> LapCompleted;
        event Action<PositionChangedEvent> PositionChanged;
        event Action<RaceStartedEvent> RaceStarted;
        event Action<RacerFinishedEvent> RacerFinished;

        RaceHudSnapshot Snapshot { get; }
        bool RacingInputActive { get; }
    }
}
