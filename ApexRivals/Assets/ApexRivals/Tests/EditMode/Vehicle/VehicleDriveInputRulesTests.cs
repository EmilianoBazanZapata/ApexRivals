using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehicleDriveInputRulesTests
    {
        [Test]
        public void LaunchSteering_RetainsTurningWithoutScrubbingAtParkingLock()
        {
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 0f, 90f, true, false), Is.EqualTo(28f));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(-80f, 0f, 90f, true, false), Is.EqualTo(-28f));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(15f, 0f, 90f, true, false), Is.EqualTo(15f));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 4f, 90f, true, false), Is.GreaterThan(28f).And.LessThan(80f));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, -4f, 90f, true, false), Is.EqualTo(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 4f, 90f, true, false)));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 8f, 90f, true, false), Is.EqualTo(80f));
        }

        [Test]
        public void LaunchSteering_PreservesDriftParkingAndSmallerConfiguredLimits()
        {
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 0f, 90f, true, true), Is.EqualTo(80f));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 0f, 90f, false, false), Is.EqualTo(80f));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 0f, 20f, true, false), Is.EqualTo(20f));
            Assert.That(VehicleDriveInputRules.LimitLaunchSteeringAngle(80f, 0f, 0f, true, false), Is.Zero);
        }

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
        [TestCase(false, 1f, false)]
        [TestCase(true, .3f, false)]
        [TestCase(true, .6f, true)]
        [TestCase(true, 2f, true)]
        public void OutsideTrackRecoveryRequiresAPersistentExcursion(bool outside, float time, bool expected)
        {
            Assert.That(VehicleRoofRecoveryRules.CanRecoverOutsideTrack(outside, time, .6f), Is.EqualTo(expected));
        }

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
