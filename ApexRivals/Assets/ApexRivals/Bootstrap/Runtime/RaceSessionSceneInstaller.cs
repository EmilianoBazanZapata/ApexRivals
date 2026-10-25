using ApexRivals.AI.Runtime;
using ApexRivals.Career.Runtime;
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
        [SerializeField] private ApexRivals.Camera.Runtime.PreRacePresentation introduction;

        public bool PlayIntroduction { get; set; } = true;
        public ApexRivals.Camera.Runtime.PreRacePresentation Introduction => introduction;

        public RaceSessionCoordinator Session { get; private set; }
        public RaceSessionCommandResult LastInstallResult { get; private set; }

        /// <summary>Career identity of the race in progress; null without a career.</summary>
        public CareerRaceAttempt CareerAttempt { get; private set; }

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

            // The attempt identity (circuit actually loaded, car, final or not) is fixed here, before setup.
            CareerAttempt = context.Career?.BeginAttempt(gameObject.scene.name, context.SelectedVehicleState.SelectedVehicleId);
            var activeSetup = CareerAttempt != null && CareerAttempt.IsFinale && context.Career.FinaleRaceSetup != null
                ? context.Career.FinaleRaceSetup
                : raceSetupDefinition;
            var setupService = new RaceSetupService(
                context.VehicleCatalog,
                context.ProgressionState,
                engineUpgradeDefinition.CreateData(),
                handlingUpgradeDefinition.CreateData(),
                new UnityRaceVehicleFactory(),
                activeSetup.CreateAiConfigurationMap(),
                activeSetup.UsePerOpponentDifficulty ? null : context.RaceProgressionService);
            var setup = new RaceSetupSessionAdapter(
                setupService,
                activeSetup,
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
                context.PauseCoordinator.CreateSession(),
                context.RaceProgressionService,
                context.Career,
                CareerAttempt);

            LastInstallResult = Session.Prepare();
            if (LastInstallResult.Succeeded)
            {
                if (PlayIntroduction && introduction != null && introduction.isActiveAndEnabled)
                    introduction.Begin(context, Session, raceCoordinator);
                else
                    LastInstallResult = Session.Start();
            }
        }

        public void Uninstall()
        {
            if (introduction != null) introduction.Cancel();
            Session?.Dispose();
            Session = null;
            CareerAttempt = null;
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
