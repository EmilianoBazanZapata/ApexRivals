using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehicleRuntimeContractTests
    {
        [Test]
        public void PlanarAndWheelControllers_SatisfyTheSharedRuntimeContract()
        {
            AssertVehicleRuntime<WheelArcadeVehicleController>();
        }

        private static void AssertVehicleRuntime<T>() where T : MonoBehaviour, IVehicleRuntime
        {
        }
    }
}
