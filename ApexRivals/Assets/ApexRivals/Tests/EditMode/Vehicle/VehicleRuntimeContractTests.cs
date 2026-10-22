using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehicleRuntimeContractTests
    {
        [Test]
        public void WheelController_ImplementsRuntimeAndTelemetryContracts()
        {
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(WheelArcadeVehicleController)), Is.True);
            Assert.That(typeof(IVehicleRuntime).IsAssignableFrom(typeof(WheelArcadeVehicleController)), Is.True);
            Assert.That(typeof(IVehicleTelemetry).IsAssignableFrom(typeof(WheelArcadeVehicleController)), Is.True);
        }
    }
}
