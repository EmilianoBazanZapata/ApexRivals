using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    public static class VehicleRuntimeResolver
    {
        public static bool TryResolveExactlyOne(
            Component owner,
            MonoBehaviour serializedRuntimeComponent,
            out IVehicleRuntime vehicleRuntime,
            out string message)
        {
            vehicleRuntime = null;

            if (owner == null)
            {
                message = "IVehicleRuntime resolution requires a component owner.";
                return false;
            }

            if (serializedRuntimeComponent != null
                && serializedRuntimeComponent.gameObject != owner.gameObject)
            {
                message = $"{owner.GetType().Name} on '{owner.gameObject.name}' requires its IVehicleRuntime reference to be on the same GameObject.";
                return false;
            }

            if (serializedRuntimeComponent != null
                && !(serializedRuntimeComponent is IVehicleRuntime))
            {
                message = $"{owner.GetType().Name} on '{owner.gameObject.name}' references {serializedRuntimeComponent.GetType().Name}, which does not implement IVehicleRuntime.";
                return false;
            }

            var components = owner.GetComponents<MonoBehaviour>();
            MonoBehaviour resolvedComponent = null;
            var runtimeCount = 0;

            for (var index = 0; index < components.Length; index++)
            {
                if (!(components[index] is IVehicleRuntime))
                {
                    continue;
                }

                runtimeCount++;
                resolvedComponent = components[index];
            }

            if (runtimeCount != 1)
            {
                message = $"{owner.GetType().Name} on '{owner.gameObject.name}' requires exactly one IVehicleRuntime on the same GameObject, found {runtimeCount}.";
                return false;
            }

            if (serializedRuntimeComponent != null && serializedRuntimeComponent != resolvedComponent)
            {
                message = $"{owner.GetType().Name} on '{owner.gameObject.name}' references {serializedRuntimeComponent.GetType().Name}, but the only IVehicleRuntime is {resolvedComponent.GetType().Name}.";
                return false;
            }

            vehicleRuntime = resolvedComponent as IVehicleRuntime;
            message = string.Empty;
            return true;
        }
    }
}
