using ApexRivals.SceneFlow.Runtime;
using UnityEngine;

namespace ApexRivals.SceneFlow.Configuration
{
    [CreateAssetMenu(menuName = "Apex Rivals/Scene Flow/Scene Flow Configuration")]
    public sealed class SceneFlowConfiguration : ScriptableObject
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string garageSceneName = "Garage";
        [SerializeField] private string raceSceneName = "Race";

        public SceneFlowSceneMap CreateSceneMap()
        {
            return new SceneFlowSceneMap(mainMenuSceneName, garageSceneName, raceSceneName);
        }
    }
}
