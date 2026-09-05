using System;

namespace ApexRivals.SceneFlow.Runtime
{
    public sealed class SceneFlowSceneMap
    {
        private readonly string _mainMenuSceneName;
        private readonly string _garageSceneName;
        private readonly string _raceSceneName;

        public SceneFlowSceneMap(string mainMenuSceneName, string garageSceneName, string raceSceneName)
        {
            _mainMenuSceneName = mainMenuSceneName;
            _garageSceneName = garageSceneName;
            _raceSceneName = raceSceneName;
        }

        public bool TryGetSceneName(ContentSceneId sceneId, out string sceneName)
        {
            sceneName = sceneId switch
            {
                ContentSceneId.MainMenu => _mainMenuSceneName,
                ContentSceneId.Garage => _garageSceneName,
                ContentSceneId.Race => _raceSceneName,
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(sceneName);
        }
    }
}
