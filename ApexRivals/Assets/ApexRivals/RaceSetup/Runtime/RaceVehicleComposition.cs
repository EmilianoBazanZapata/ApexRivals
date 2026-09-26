using System;
using ApexRivals.AI.Configuration;
using ApexRivals.AI.Runtime;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    public sealed class RaceVehicleComposition : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour vehicleController;
        [SerializeField] private VehicleResetter vehicleResetter;
        [SerializeField] private RaceParticipant raceParticipant;
        [SerializeField] private DrivingInputGate drivingInputGate;
        [SerializeField] private MonoBehaviour playerInputProvider;
        [SerializeField] private AiDrivingInputProvider aiInputProvider;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform progressTransform;
        [SerializeField] private Transform aiRecoveryPose;

        private IVehicleRuntime _vehicleRuntime;

        public IVehicleRuntime VehicleRuntime => _vehicleRuntime;
        public IVehicleTelemetry VehicleTelemetry => vehicleController as IVehicleTelemetry;
        public VehicleResetter VehicleResetter => vehicleResetter;
        public RaceParticipant RaceParticipant => raceParticipant;
        public DrivingInputGate DrivingInputGate => drivingInputGate;
        public Rigidbody VehicleRigidbody => GetComponent<Rigidbody>();
        public AiDrivingInputProvider AiInputProvider => aiInputProvider;
        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;

        public void ConfigureReferences(
            MonoBehaviour controller,
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
            _ = groundOrigin;
            vehicleController = controller;
            _vehicleRuntime = vehicleController as IVehicleRuntime;
            vehicleResetter = resetBoundary;
            raceParticipant = participant;
            drivingInputGate = inputGate;
            playerInputProvider = playerInput;
            aiInputProvider = aiInput;
            cameraTarget = target;
            progressTransform = participantProgressTransform;
            aiRecoveryPose = recoveryPose;
        }

        public bool CanConfigurePlayer => TryValidatePlayer(out _);

        public bool CanConfigureAI => TryValidateAI(out _);

        public void ConfigurePlayer(
            RaceCoordinator raceCoordinator,
            string participantId,
            VehiclePerformanceStats effectiveStats)
        {
            if (!TryValidatePlayer(out var validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            var inputProvider = playerInputProvider as IDrivingInputProvider;
            drivingInputGate.Configure(inputProvider, false);
            raceParticipant.Configure(raceCoordinator, participantId, progressTransform != null ? progressTransform : transform);
            _vehicleRuntime.ConfigureRuntime(drivingInputGate, vehicleResetter);
            _vehicleRuntime.ApplyPerformanceStats(effectiveStats);
        }

        public void ConfigureAI(
            RaceCoordinator raceCoordinator,
            string participantId,
            AiDriverConfiguration aiConfiguration,
            RacingLine racingLine)
        {
            if (!TryValidateAI(out var validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            aiInputProvider.Configure(
                aiConfiguration,
                racingLine,
                _vehicleRuntime,
                vehicleResetter,
                raceCoordinator,
                raceParticipant,
                transform,
                aiRecoveryPose);
            drivingInputGate.Configure(aiInputProvider, false);
            raceParticipant.Configure(raceCoordinator, participantId, progressTransform != null ? progressTransform : transform);
            _vehicleRuntime.ConfigureRuntime(drivingInputGate, vehicleResetter);
        }

        private void Awake()
        {
            TryResolveVehicleRuntime(out _);
        }

        public bool TryValidatePlayer(out string message)
        {
            var isValid = RaceVehicleCompositionValidator.TryValidatePlayer(
                CreateValidationContext(),
                out var vehicleRuntime,
                out message);
            return CacheValidationResult(isValid, vehicleRuntime);
        }

        public bool TryValidateAI(out string message)
        {
            var isValid = RaceVehicleCompositionValidator.TryValidateAI(
                CreateValidationContext(),
                out var vehicleRuntime,
                out message);
            return CacheValidationResult(isValid, vehicleRuntime);
        }

        private bool TryResolveVehicleRuntime(out string message)
        {
            if (!VehicleRuntimeResolver.TryResolveExactlyOne(this, vehicleController, out var runtime, out message))
            {
                _vehicleRuntime = null;
                return false;
            }

            _vehicleRuntime = runtime;
            vehicleController = runtime as MonoBehaviour;
            return true;
        }

        private RaceVehicleCompositionValidationContext CreateValidationContext()
        {
            return new RaceVehicleCompositionValidationContext(
                this,
                vehicleController,
                vehicleResetter,
                VehicleRigidbody,
                raceParticipant,
                drivingInputGate,
                playerInputProvider,
                aiInputProvider,
                cameraTarget);
        }

        private bool CacheValidationResult(bool isValid, IVehicleRuntime vehicleRuntime)
        {
            if (vehicleRuntime == null)
            {
                _vehicleRuntime = null;
                return false;
            }

            _vehicleRuntime = vehicleRuntime;
            vehicleController = vehicleRuntime as MonoBehaviour;
            return isValid;
        }
    }
}
