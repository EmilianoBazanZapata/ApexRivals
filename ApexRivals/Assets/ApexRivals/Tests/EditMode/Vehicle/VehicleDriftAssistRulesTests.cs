using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehicleDriftAssistRulesTests
    {
        private const float MaximumSlip = 40f;

        private static float Assist(float slipAngle, float yawRate)
        {
            return VehicleDriftAssistRules.CalculateYawAcceleration(slipAngle, yawRate, MaximumSlip, 0.25f, 4f, 8f);
        }

        [Test]
        public void ModerateSlide_IsLeftToTheDriver()
        {
            Assert.That(Assist(20f, 2f), Is.EqualTo(0f));
            Assert.That(Assist(-20f, -2f), Is.EqualTo(0f));
        }

        [Test]
        public void RotationGrowingTheSlide_IsDampedNearTheMaximum()
        {
            Assert.That(Assist(36f, 1.5f), Is.LessThan(0f), "Nose swinging further right: yaw it back left.");
            Assert.That(Assist(-36f, -1.5f), Is.GreaterThan(0f), "Mirrored.");
        }

        [Test]
        public void CarAlreadyRecovering_IsNotPushedFurther()
        {
            Assert.That(Assist(36f, -1.5f), Is.EqualTo(0f));
        }

        [Test]
        public void SlideBeyondTheMaximum_IsPulledBackWithinTheCap()
        {
            Assert.That(Assist(55f, 0f), Is.LessThan(0f));
            Assert.That(Assist(120f, 10f), Is.EqualTo(-8f), "Never stronger than the cap.");
        }
    }
}
