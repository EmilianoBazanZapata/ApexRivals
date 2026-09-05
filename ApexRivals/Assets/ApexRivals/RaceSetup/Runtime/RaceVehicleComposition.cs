using ApexRivals.AI.Configuration;
using ApexRivals.AI.Runtime;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    public sealed class RaceVehicleComposition : MonoBehaviour
    {
        [SerializeField] private ArcadeVehicleController vehicleController;
        [SerializeField] private VehicleResetter vehicleResetter;
        [SerializeField] private RaceParticipant raceParticipant;
        [SerializeField] private DrivingInputGate drivingInputGate;
        [SerializeField] private MonoBehaviour playerInputProvider;
        [SerializeField] private AiDrivingInputProvider aiInputProvider;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform progressTransform;
        [SerializeField] private Transform groundCheckOrigin;
        [SerializeField] private Transform aiRecoveryPose;

        public ArcadeVehicleController VehicleController => vehicleController;
        public VehicleResetter VehicleResetter => vehicleResetter;
        public RaceParticipant RaceParticipant => raceParticipant;
        public DrivingInputGate DrivingInputGate => drivingInputGate;
        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;

        public void ConfigureReferences(
            ArcadeVehicleController controller,
            VehicleResetter resetBoundary,
            RaceParticipant participant,
            DrivingInputGate inputGate,
            MonoBehaviour playerInput,
            AiDrivingInputProvider aiInput,
            Transform target,
            Transform participantProgressTransform,
            Transform groundOrigin,
            Transform recoveryPose)
        {
            vehicleController = controller;
            vehicleResetter = resetBoundary;
            raceParticipant = participant;
            drivingInputGate = inputGate;
            playerInputProvider = playerInput;
            aiInputProvider = aiInput;
            cameraTarget = target;
            progressTransform = participantProgressTransform;
            groundCheckOrigin = groundOrigin;
            aiRecoveryPose = recoveryPose;
        }

        public bool CanConfigurePlayer => vehicleController != null
            && raceParticipant != null
            && drivingInputGate != null
            && playerInputProvider is IDrivingInputProvider;

        public bool CanConfigureAI => vehicleController != null
            && raceParticipant != null
            && drivingInputGate != null
            && aiInputProvider != null;

        public void ConfigurePlayer(
            RaceCoordinator raceCoordinator,
            string participantId,
            ArcadeVehicleConfiguration baseConfiguration,
            ArcadeVehicleStats effectiveStats)
        {
            var inputProvider = playerInputProvider as IDrivingInputProvider;
            drivingInputGate.Configure(inputProvider, false);
            raceParticipant.Configure(raceCoordinator, participantId, progressTransform != null ? progressTransform : transform);
            vehicleController.Configure(baseConfiguration, drivingInputGate, vehicleResetter, groundCheckOrigin);
            vehicleController.ApplyEffectiveStats(effectiveStats);
        }

        public void ConfigureAI(
            RaceCoordinator raceCoordinator,
            string participantId,
            ArcadeVehicleConfiguration baseConfiguration,
            AiDriverConfiguration aiConfiguration,
            RacingLine racingLine)
        {
            aiInputProvider.Configure(
                aiConfiguration,
                racingLine,
                vehicleController,
                vehicleResetter,
                raceCoordinator,
                raceParticipant,
                transform,
                aiRecoveryPose);
            drivingInputGate.Configure(aiInputProvider, false);
            raceParticipant.Configure(raceCoordinator, participantId, progressTransform != null ? progressTransform : transform);
            vehicleController.Configure(baseConfiguration, drivingInputGate, vehicleResetter, groundCheckOrigin);
        }
    }
}
