using ApexRivals.AI.Runtime;
using ApexRivals.Garage.Configuration;
using ApexRivals.Progression.Configuration;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Configuration;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.RaceSession.Runtime;
using UnityEngine;

namespace ApexRivals.Bootstrap.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceSessionSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private RaceSetupDefinition raceSetupDefinition;
        [SerializeField] private RaceRewardDefinition raceRewardDefinition;
        [SerializeField] private UpgradeDefinition engineUpgradeDefinition;
        [SerializeField] private UpgradeDefinition handlingUpgradeDefinition;
        [SerializeField] private RaceCoordinator raceCoordinator;
        [SerializeField] private StartingGrid startingGrid;
        [SerializeField] private RacingLine racingLine;

        public RaceSessionCoordinator Session { get; private set; }
        public RaceSessionCommandResult LastInstallResult { get; private set; }

        public void Install(ApplicationContext context)
        {
            if (context == null)
            {
                LastInstallResult = RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingConfiguration, "Application context is missing.");
                return;
            }

            var validation = Validate(context);
            if (!validation.Succeeded)
            {
                LastInstallResult = validation;
                return;
            }

            var setupService = new RaceSetupService(
                context.VehicleCatalog,
                context.ProgressionState,
                engineUpgradeDefinition.CreateData(),
                handlingUpgradeDefinition.CreateData(),
                new UnityRaceVehicleFactory(),
                raceSetupDefinition.CreateAiConfigurationMap(),
                context.RaceProgressionService);
            var setup = new RaceSetupSessionAdapter(
                setupService,
                raceSetupDefinition,
                context.SelectedVehicleState,
                startingGrid,
                raceCoordinator,
                racingLine);
            var rewardService = new RaceRewardService(context.ProgressionState, raceRewardDefinition.CreateRewardTable());
            var saveCheckpoint = new PlayerProfileSaveCheckpoint(context.SaveService);
            var navigation = new SceneFlowRaceSessionNavigation(context.Navigation);
            Session = new RaceSessionCoordinator(
                setup,
                new RaceCoordinatorSessionAdapter(raceCoordinator),
                rewardService,
                context.ProgressionState,
                saveCheckpoint,
                navigation,
                context.Navigation.RaceSession,
                new UnityTimeScaleController(),
                context.RaceProgressionService);

            LastInstallResult = Session.Prepare();
            if (LastInstallResult.Succeeded)
            {
                LastInstallResult = Session.Start();
            }
        }

        public void Uninstall()
        {
            Session?.Dispose();
            Session = null;
        }

        private RaceSessionCommandResult Validate(ApplicationContext context)
        {
            if (context.VehicleCatalog == null)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingConfiguration, "Vehicle catalog is missing.");
            }

            if (context.ProgressionState == null || context.SelectedVehicleState == null || context.SaveService == null || context.Navigation == null)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingConfiguration, "Required runtime services are missing.");
            }

            if (context.RaceProgressionService == null)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingConfiguration, "Race progression service is missing.");
            }

            var progressionValidation = context.RaceProgressionService.Validate();
            if (!progressionValidation.Succeeded)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingConfiguration, progressionValidation.Message);
            }

            if (raceSetupDefinition == null || raceRewardDefinition == null || engineUpgradeDefinition == null || handlingUpgradeDefinition == null)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingConfiguration, "Race session authored configuration is incomplete.");
            }

            if (raceCoordinator == null)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingRaceCoordinator, "Race coordinator is missing.");
            }

            if (startingGrid == null)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingConfiguration, "Starting grid is missing.");
            }

            return RaceSessionCommandResult.Success();
        }
    }
}
