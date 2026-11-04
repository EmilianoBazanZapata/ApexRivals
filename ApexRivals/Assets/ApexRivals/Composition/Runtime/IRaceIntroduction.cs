using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;

namespace ApexRivals.Composition.Runtime
{
    /// <summary>
    /// Pre-race presentation (camera shots over the grid) that starts the session when it finishes. Implemented by the
    /// UI module so the race session installer does not depend on presentation code.
    /// </summary>
    public interface IRaceIntroduction
    {
        bool IsAvailable { get; }

        /// <summary>True while race commands (pause, HUD input) must stay blocked by the presentation.</summary>
        bool SuppressCommands { get; }
        void Begin(ApplicationContext context, RaceSessionCoordinator session, RaceCoordinator coordinator);
        void Cancel();
    }
}
