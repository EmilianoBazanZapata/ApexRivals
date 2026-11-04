using System;
using ApexRivals.RaceSession.Runtime;

namespace ApexRivals.UI.Runtime
{
    public interface IRaceHudSource
    {
        event Action RefreshRequested;

        RaceHudSnapshot Snapshot { get; }
        bool RacingInputActive { get; }
    }
}
