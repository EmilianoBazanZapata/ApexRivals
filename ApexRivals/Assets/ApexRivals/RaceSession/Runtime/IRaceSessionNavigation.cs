using System.Threading.Tasks;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.RaceSession.Runtime
{
    public interface IRaceSessionNavigation
    {
        Task<SceneTransitionResult> RetryRace();

        Task<SceneTransitionResult> ReturnToGarage();

        Task<SceneTransitionResult> ReturnToMainMenu();

        bool ShowResults(RaceSessionResult result);

        bool PauseRace();

        bool ResumeRace();
    }
}
