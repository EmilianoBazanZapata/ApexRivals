using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehiclePerformanceTuningRulesTests
    {
        [Test]
        public void IdentityStatsPreservePhysicsAndRepeatedCalculationDoesNotCompound()
        {
            var tuning = new WheelVehicleTuning
            {
                motorPower = 100f,
                frontSidewaysFriction = new WheelFrictionSettings { stiffness = 1f },
                rearSidewaysFriction = new WheelFrictionSettings { stiffness = 0.8f },
                driftRearSidewaysGripMultiplier = 0.5f
            };
            var baseline = new VehiclePerformanceStats(12f, 50f, 10f, 0.5f, 0.2f);
            var identity = VehiclePerformanceTuningRules.Apply(tuning, baseline, baseline);
            Assert.That(identity.motorPower, Is.EqualTo(100f));
            Assert.That(identity.rearSidewaysFriction.stiffness, Is.EqualTo(0.8f));
            var upgraded = new VehiclePerformanceStats(15f, 56f, 12.5f, 0.6f, 0.28f);
            var first = VehiclePerformanceTuningRules.Apply(tuning, baseline, upgraded);
            var second = VehiclePerformanceTuningRules.Apply(tuning, baseline, upgraded);
            Assert.That(first.motorPower, Is.EqualTo(125f));
            Assert.That(second.motorPower, Is.EqualTo(first.motorPower));
            Assert.That(first.rearSidewaysFriction.stiffness * first.driftRearSidewaysGripMultiplier,
                Is.EqualTo(0.8f * 0.5f * 1.4f).Within(0.0001f));
            Assert.That(tuning.rearSidewaysFriction.stiffness, Is.EqualTo(0.8f));
        }

        [TestCase(0f, 5f)]
        [TestCase(5f, 0f)]
        [TestCase(5f, float.NaN)]
        [TestCase(5f, float.PositiveInfinity)]
        public void InvalidRatingsUseANeutralMultiplier(float baseline, float effective)
        {
            Assert.That(VehiclePerformanceTuningRules.RelativeMultiplier(baseline, effective), Is.EqualTo(1f));
        }

        [Test]
        public void HigherSpeedLimitRetainsPropulsionBeyondTheBaseLimitWithoutLimitingReverse()
        {
            Assert.That(VehiclePerformanceTuningRules.ForwardMotorFactor(50f, 50f), Is.Zero);
            Assert.That(VehiclePerformanceTuningRules.ForwardMotorFactor(50f, 56f), Is.EqualTo(1f));
            Assert.That(VehiclePerformanceTuningRules.ForwardMotorFactor(-50f, 50f), Is.EqualTo(1f));
        }
    }
}
