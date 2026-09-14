using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehicleDriveInputRulesTests
    {
        [Test]
        public void ResolveInputs_UsesForwardTorqueAtTheReverseEntryBoundary()
        {
            var brakeInput = VehicleDriveInputRules.ResolveNormalBrakeInput(1f, 0f, -0.75f, 0.75f);
            var motorInput = VehicleDriveInputRules.ResolveMotorInput(1f, 0f, -0.75f, 0.75f);

            Assert.That(brakeInput, Is.EqualTo(0f));
            Assert.That(motorInput, Is.EqualTo(1f));
        }

        [Test]
        public void ResolveInputs_UsesThrottleAsBrakeWhenMovingBackwardBeyondTheEntryThreshold()
        {
            var brakeInput = VehicleDriveInputRules.ResolveNormalBrakeInput(1f, 0f, -0.751f, 0.75f);
            var motorInput = VehicleDriveInputRules.ResolveMotorInput(1f, 0f, -0.751f, 0.75f);

            Assert.That(brakeInput, Is.EqualTo(1f));
            Assert.That(motorInput, Is.EqualTo(0f));
        }

        [Test]
        public void ResolveInputs_UsesBrakeAsReverseTorqueAtTheReverseEntryBoundary()
        {
            var brakeInput = VehicleDriveInputRules.ResolveNormalBrakeInput(0f, 1f, 0.75f, 0.75f);
            var motorInput = VehicleDriveInputRules.ResolveMotorInput(0f, 1f, 0.75f, 0.75f);

            Assert.That(brakeInput, Is.EqualTo(0f));
            Assert.That(motorInput, Is.EqualTo(-1f));
        }

        [Test]
        public void ResolveInputs_CoastsWithoutThrottleOrBrake()
        {
            var brakeInput = VehicleDriveInputRules.ResolveNormalBrakeInput(0f, 0f, 0f, 0.75f);
            var motorInput = VehicleDriveInputRules.ResolveMotorInput(0f, 0f, 0f, 0.75f);

            Assert.That(brakeInput, Is.EqualTo(0f));
            Assert.That(motorInput, Is.EqualTo(0f));
        }
    }

    public sealed class VehicleRoofRecoveryRulesTests
    {
        [Test]
        public void UprightVehicle_CannotRecover()
        {
            Assert.That(VehicleRoofRecoveryRules.CanRecover(false, true, 1f, 0.2f), Is.False);
        }

        [Test]
        public void InvertedVehicleWithoutRoofSurface_CannotRecover()
        {
            Assert.That(VehicleRoofRecoveryRules.CanRecover(true, false, 1f, 0.2f), Is.False);
        }

        [Test]
        public void InvertedVehicleOnRoofAfterDebounce_CanRecover()
        {
            Assert.That(VehicleRoofRecoveryRules.IsInverted(-0.6f, -0.5f), Is.True);
            Assert.That(VehicleRoofRecoveryRules.CanRecover(true, true, 0.2f, 0.2f), Is.True);
        }

        [TestCase(RecoveryInputSource.KeyboardE)]
        [TestCase(RecoveryInputSource.GamepadView)]
        public void RequestedRecovery_UsesEitherRequestedInput(RecoveryInputSource inputSource)
        {
            Assert.That(VehicleRoofRecoveryRules.ShouldRequestRecovery(true, inputSource), Is.True);
        }

        [Test]
        public void InputWhenRecoveryUnavailable_DoesNotRequestReset()
        {
            Assert.That(VehicleRoofRecoveryRules.ShouldRequestRecovery(false, RecoveryInputSource.KeyboardE), Is.False);
        }
    }
}
