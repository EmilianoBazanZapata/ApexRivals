using System.Collections;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Configuration;
using ApexRivals.SceneFlow.Runtime;
using UnityEngine;

namespace ApexRivals.Bootstrap.Runtime
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private SceneFlowConfiguration sceneFlowConfiguration;
        [SerializeField] private UnityContentSceneLoader sceneLoader;
        [SerializeField] private string[] knownVehicleIds = { SelectedVehicleState.DefaultVehicleId };

        public ApplicationContext Context { get; private set; }
        public BootstrapStartupStatus StartupStatus { get; private set; } = BootstrapStartupStatus.NotStarted;

        private IEnumerator Start()
        {
            var startTask = StartAsync();
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            if (startTask.IsFaulted)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                Debug.LogException(startTask.Exception);
            }
        }

        public System.Threading.Tasks.Task<BootstrapStartupResult> StartAsync()
        {
            if (sceneFlowConfiguration == null)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                return System.Threading.Tasks.Task.FromResult(new BootstrapStartupResult(BootstrapStartupStatus.Failed, "Scene flow configuration is not assigned."));
            }

            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<UnityContentSceneLoader>();
            }

            if (sceneLoader == null)
            {
                sceneLoader = gameObject.AddComponent<UnityContentSceneLoader>();
            }

            var progressionState = new PlayerProgressionState();
            var selectedVehicleState = new SelectedVehicleState();
            var storage = new PlayerProfileFileStorage(Application.persistentDataPath);
            var saveService = new PlayerProfileSaveService(progressionState, selectedVehicleState, storage, knownVehicleIds);
            var sceneFlowService = new SceneFlowService(
                sceneFlowConfiguration.CreateSceneMap(),
                sceneLoader,
                new PlayerProfileSaveCheckpoint(saveService));

            Context = new ApplicationContext(sceneFlowService, progressionState, selectedVehicleState, saveService);

            var bootstrapService = new ApplicationBootstrapService(saveService, sceneFlowService);
            var startTask = bootstrapService.StartAsync();
            return CompleteStartupAsync(startTask, bootstrapService);
        }

        private async System.Threading.Tasks.Task<BootstrapStartupResult> CompleteStartupAsync(
            System.Threading.Tasks.Task<BootstrapStartupResult> startTask,
            ApplicationBootstrapService bootstrapService)
        {
            var result = await startTask;
            StartupStatus = bootstrapService.StartupStatus;
            if (!result.Succeeded)
            {
                Debug.LogError(result.Message);
            }

            return result;
        }
    }
}
