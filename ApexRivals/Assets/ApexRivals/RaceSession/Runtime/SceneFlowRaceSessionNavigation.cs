using System;
using System.Threading.Tasks;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.RaceSession.Runtime
{
    public sealed class SceneFlowRaceSessionNavigation : IRaceSessionNavigation
    {
        private readonly SceneFlowService _sceneFlow;

        public SceneFlowRaceSessionNavigation(SceneFlowService sceneFlow)
        {
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public Task<SceneTransitionResult> RetryRace()
        {
            return _sceneFlow.RetryRace();
        }

        public Task<SceneTransitionResult> ReturnToGarage()
        {
            return _sceneFlow.ReturnFromRaceToGarage();
        }

        public Task<SceneTransitionResult> ReturnToMainMenu()
        {
            return _sceneFlow.ReturnFromRaceToMainMenu();
        }

        public bool ShowResults()
        {
            return _sceneFlow.ShowStoredRaceResults();
        }

        public bool PauseRace()
        {
            return _sceneFlow.PauseRace();
        }

        public bool ResumeRace()
        {
            return _sceneFlow.ResumeRace();
        }
    }
}
