using ApexRivals.AI.Runtime;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    internal readonly struct RaceVehicleCompositionValidationContext
    {
        public RaceVehicleCompositionValidationContext(
            Component owner,
            MonoBehaviour serializedRuntimeComponent,
            VehicleResetter vehicleResetter,
            Rigidbody vehicleRigidbody,
            RaceParticipant raceParticipant,
            DrivingInputGate drivingInputGate,
            MonoBehaviour playerInputProvider,
            AiDrivingInputProvider aiInputProvider,
            Transform cameraTarget)
        {
            Owner = owner;
            SerializedRuntimeComponent = serializedRuntimeComponent;
            VehicleResetter = vehicleResetter;
            VehicleRigidbody = vehicleRigidbody;
            RaceParticipant = raceParticipant;
            DrivingInputGate = drivingInputGate;
            PlayerInputProvider = playerInputProvider;
            AiInputProvider = aiInputProvider;
            CameraTarget = cameraTarget;
        }

        public Component Owner { get; }
        public MonoBehaviour SerializedRuntimeComponent { get; }
        public VehicleResetter VehicleResetter { get; }
        public Rigidbody VehicleRigidbody { get; }
        public RaceParticipant RaceParticipant { get; }
        public DrivingInputGate DrivingInputGate { get; }
        public MonoBehaviour PlayerInputProvider { get; }
        public AiDrivingInputProvider AiInputProvider { get; }
        public Transform CameraTarget { get; }
    }

    internal static class RaceVehicleCompositionValidator
    {
        public static bool TryValidatePlayer(
            RaceVehicleCompositionValidationContext context,
            out IVehicleRuntime vehicleRuntime,
            out string message)
        {
            if (!TryValidateCommon(context, true, out vehicleRuntime, out message))
            {
                return false;
            }

            if (!(context.PlayerInputProvider is PlayerDrivingInput))
            {
                var actualType = context.PlayerInputProvider != null
                    ? context.PlayerInputProvider.GetType().Name
                    : "no input provider";
                message = $"Player RaceVehicleComposition on '{context.Owner.gameObject.name}' requires PlayerDrivingInput, found {actualType}.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public static bool TryValidateAI(
            RaceVehicleCompositionValidationContext context,
            out IVehicleRuntime vehicleRuntime,
            out string message)
        {
            if (!TryValidateCommon(context, false, out vehicleRuntime, out message))
            {
                return false;
            }

            if (context.AiInputProvider == null)
            {
                message = $"AI RaceVehicleComposition on '{context.Owner.gameObject.name}' requires AiDrivingInputProvider.";
                return false;
            }

            return context.AiInputProvider.TryValidateVehicleRuntimeReference(out message);
        }

        private static bool TryValidateCommon(
            RaceVehicleCompositionValidationContext context,
            bool requireCameraTarget,
            out IVehicleRuntime vehicleRuntime,
            out string message)
        {
            if (!VehicleRuntimeResolver.TryResolveExactlyOne(
                    context.Owner,
                    context.SerializedRuntimeComponent,
                    out vehicleRuntime,
                    out message))
            {
                return false;
            }

            if (context.VehicleResetter == null)
            {
                message = $"RaceVehicleComposition on '{context.Owner.gameObject.name}' requires VehicleResetter.";
                return false;
            }

            if (!context.VehicleResetter.TryValidateConfiguration(out message))
            {
                return false;
            }

            if (context.VehicleRigidbody == null)
            {
                message = $"RaceVehicleComposition on '{context.Owner.gameObject.name}' requires a Rigidbody for vehicle and camera runtime setup.";
                return false;
            }

            if (context.RaceParticipant == null)
            {
                message = $"RaceVehicleComposition on '{context.Owner.gameObject.name}' requires RaceParticipant.";
                return false;
            }

            if (context.DrivingInputGate == null)
            {
                message = $"RaceVehicleComposition on '{context.Owner.gameObject.name}' requires DrivingInputGate.";
                return false;
            }

            if (requireCameraTarget && context.CameraTarget == null)
            {
                message = $"Player RaceVehicleComposition on '{context.Owner.gameObject.name}' requires a CameraTarget.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
