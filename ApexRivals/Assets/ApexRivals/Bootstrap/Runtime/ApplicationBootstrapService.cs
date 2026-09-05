using System;
using System.Threading.Tasks;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Settings.Runtime;

namespace ApexRivals.Bootstrap.Runtime
{
    public sealed class ApplicationBootstrapService
    {
        private readonly PlayerProfileSaveService _saveService;
        private readonly SceneFlowService _navigation;
        private readonly SettingsService _settingsService;

        public ApplicationBootstrapService(PlayerProfileSaveService saveService, SceneFlowService navigation, SettingsService settingsService = null)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _settingsService = settingsService;
            StartupStatus = BootstrapStartupStatus.NotStarted;
        }

        public BootstrapStartupStatus StartupStatus { get; private set; }
        public SettingsOperationResult SettingsLoadResult { get; private set; }

        public async Task<BootstrapStartupResult> StartAsync()
        {
            SettingsLoadResult = _settingsService?.Load() ?? SettingsOperationResult.Success();

            var loadResult = _saveService.LoadOrCreateDefault();
            if (!loadResult.Succeeded)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                _navigation.MarkFailed();
                return new BootstrapStartupResult(BootstrapStartupStatus.Failed, loadResult.Message);
            }

            var transitionResult = await _navigation.OpenMainMenu();
            if (!transitionResult.Succeeded)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                _navigation.MarkFailed();
                return new BootstrapStartupResult(BootstrapStartupStatus.Failed, transitionResult.Message);
            }

            StartupStatus = CreateStartupStatus(loadResult.Status);
            return new BootstrapStartupResult(StartupStatus, SettingsLoadResult.Message);
        }

        private static BootstrapStartupStatus CreateStartupStatus(PlayerProfileLoadStatus loadStatus)
        {
            return loadStatus switch
            {
                PlayerProfileLoadStatus.Loaded => BootstrapStartupStatus.LoadedProfile,
                PlayerProfileLoadStatus.CreatedDefault => BootstrapStartupStatus.CreatedDefaultProfile,
                PlayerProfileLoadStatus.RecoveredFromBackup => BootstrapStartupStatus.RecoveredFromBackup,
                _ => BootstrapStartupStatus.Failed
            };
        }
    }
}
