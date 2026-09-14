using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    public interface IRaceVehicleFactory
    {
        bool TryCreate(GameObject prefab, SpawnPose pose, out RaceVehicleComposition composition, out string message);
        void Destroy(RaceVehicleComposition composition);
    }
}
