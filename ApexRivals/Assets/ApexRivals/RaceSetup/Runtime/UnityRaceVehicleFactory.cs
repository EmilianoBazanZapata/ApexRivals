using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    public sealed class UnityRaceVehicleFactory : IRaceVehicleFactory
    {
        public bool TryCreate(GameObject prefab, SpawnPose pose, out RaceVehicleComposition composition, out string message)
        {
            composition = null;
            if (prefab == null)
            {
                message = "The vehicle prefab is missing.";
                return false;
            }

            var instance = Object.Instantiate(prefab, pose.Position, pose.Rotation);
            composition = instance.GetComponent<RaceVehicleComposition>();
            if (composition == null)
            {
                Object.Destroy(instance);
                message = "The vehicle prefab root is missing RaceVehicleComposition.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public void Destroy(RaceVehicleComposition composition)
        {
            if (composition != null)
            {
                Object.Destroy(composition.gameObject);
            }
        }
    }
}
