using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehicleDrivetrainRulesTests
    {
        [Test]
        public void CalculateAverageDrivenWheelRpm_UsesAbsoluteRearWheelSpeeds()
        {
            var averageRpm = VehicleDrivetrainRules.CalculateAverageDrivenWheelRpm(-200f, 100f);

            Assert.That(averageRpm, Is.EqualTo(150f));
        }

        [Test]
        public void EvaluateCoastingDownshift_DropsOneGearWhenRpmFallsWhileRollingForward()
        {
            var decision = VehicleDrivetrainRules.EvaluateCoastingDownshift(3, 6f, 2000f, 3300f);

            Assert.That(decision.ShouldShift, Is.True);
            Assert.That(decision.RequestedGearIndex, Is.EqualTo(2));
        }

        [TestCase(0, 6f, 1000f)]
        [TestCase(3, 6f, 4000f)]
        [TestCase(3, -1f, 1000f)]
        public void EvaluateCoastingDownshift_KeepsGearInFirstAboveThresholdOrWhenNotRollingForward(int gear, float speed, float rpm)
        {
            var decision = VehicleDrivetrainRules.EvaluateCoastingDownshift(gear, speed, rpm, 3300f);

            Assert.That(decision.ShouldShift, Is.False);
            Assert.That(decision.RequestedGearIndex, Is.EqualTo(gear));
        }

        [Test]
        public void CalculateGroundWheelRpm_ConvertsForwardSpeedForTheWheelRadius()
        {
            // 10 m/s on a 0.5 m wheel: 10 / (2 * pi * 0.5) rev/s = 190.99 RPM, reversing included.
            Assert.That(VehicleDrivetrainRules.CalculateGroundWheelRpm(10f, 0.5f), Is.EqualTo(190.99f).Within(0.01f));
            Assert.That(VehicleDrivetrainRules.CalculateGroundWheelRpm(-10f, 0.5f), Is.EqualTo(190.99f).Within(0.01f));
        }

        [Test]
        public void CalculateShiftWheelRpm_IgnoresWheelspinButKeepsSlowerDrivenWheels()
        {
            Assert.That(VehicleDrivetrainRules.CalculateShiftWheelRpm(1129f, 96f), Is.EqualTo(96f));
            Assert.That(VehicleDrivetrainRules.CalculateShiftWheelRpm(-300f, 320f), Is.EqualTo(300f));
        }

        [Test]
        public void LaunchWheelspin_DoesNotUpshiftWhileTheCarIsBarelyMoving()
        {
            // Measured launch: rear wheels at 1129 RPM while the car rolls at 11 km/h (96 wheel RPM), first gear 3.0, diff 4.0.
            var spinningRpm = VehicleDrivetrainRules.CalculateRawEngineRpm(1129f, 3f, 4f);
            var shiftRpm = VehicleDrivetrainRules.CalculateRawEngineRpm(
                VehicleDrivetrainRules.CalculateShiftWheelRpm(1129f, VehicleDrivetrainRules.CalculateGroundWheelRpm(11f / 3.6f, 0.35f)), 3f, 4f);

            Assert.That(VehicleDrivetrainRules.EvaluateAutomaticGearShift(true, 0, 6, spinningRpm, 5500f, 3300f).ShouldShift, Is.True);
            Assert.That(VehicleDrivetrainRules.EvaluateAutomaticGearShift(true, 0, 6, shiftRpm, 5500f, 3300f).ShouldShift, Is.False);
        }

        [TestCase(10f, 10f)]
        [TestCase(13.9f, 10f)]
        [TestCase(1.5f, 0f)]
        public void EvaluateTractionControl_KeepsFullTorqueWithinTheSlipAllowance(float wheelSurfaceSpeed, float groundSpeed)
        {
            Assert.That(VehicleDrivetrainRules.EvaluateTractionControl(wheelSurfaceSpeed, groundSpeed, 0.2f, 2f, 0.15f), Is.EqualTo(1f));
        }

        [Test]
        public void EvaluateTractionControl_TrimsTorqueInProportionToWheelspinDownToTheFloor()
        {
            // Allowed surface speed: 10 * 1.2 + 2 = 14 m/s.
            Assert.That(VehicleDrivetrainRules.EvaluateTractionControl(28f, 10f, 0.2f, 2f, 0.15f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(VehicleDrivetrainRules.EvaluateTractionControl(-28f, -10f, 0.2f, 2f, 0.15f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(VehicleDrivetrainRules.EvaluateTractionControl(460f, 1f, 0.2f, 2f, 0.15f), Is.EqualTo(0.15f));
        }

        [Test]
        public void CalculateRawEngineRpm_AppliesGearRatioAndDifferentialMagnitude()
        {
            var engineRpm = VehicleDrivetrainRules.CalculateRawEngineRpm(100f, -3f, -4f);

            Assert.That(engineRpm, Is.EqualTo(1200f));
        }

        [Test]
        public void CalculateRearWheelTorque_PreservesTheExistingTorqueFormula()
        {
            var torque = VehicleDrivetrainRules.CalculateRearWheelTorque(1f, 100f, 500f, 3f, 4f);

            Assert.That(torque, Is.EqualTo(12604.8f).Within(0.01f));
        }

        [Test]
        public void EvaluateEngineRpm_ClampsTargetAndCurrentRpmToEngineBounds()
        {
            var evaluation = VehicleDrivetrainRules.EvaluateEngineRpm(8000f, 800f, 900f, 6000f, 0f);

            Assert.That(evaluation.TargetEngineRpm, Is.EqualTo(900f));
            Assert.That(evaluation.EngineRpm, Is.EqualTo(6000f));
        }

        [TestCase(-100f, 6000f, 0f)]
        [TestCase(3000f, 6000f, 0.5f)]
        [TestCase(9000f, 6000f, 1f)]
        public void NormalizeEngineRpm_ClampsToPowerCurveRange(float engineRpm, float redlineRpm, float expected)
        {
            var normalizedRpm = VehicleDrivetrainRules.NormalizeEngineRpm(engineRpm, redlineRpm);

            Assert.That(normalizedRpm, Is.EqualTo(expected));
        }

        [Test]
        public void EvaluateAutomaticGearShift_UpshiftsAboveTheConfiguredThreshold()
        {
            var decision = VehicleDrivetrainRules.EvaluateAutomaticGearShift(true, 1, 4, 5501f, 5500f, 3300f);

            Assert.That(decision.ShouldShift, Is.True);
            Assert.That(decision.RequestedGearIndex, Is.EqualTo(2));
        }

        [Test]
        public void EvaluateAutomaticGearShift_DownshiftsBelowTheConfiguredThresholdWithoutGoingBelowFirstGear()
        {
            var downshift = VehicleDrivetrainRules.EvaluateAutomaticGearShift(true, 1, 4, 3299f, 5500f, 3300f);
            var firstGear = VehicleDrivetrainRules.EvaluateAutomaticGearShift(true, 0, 4, 3299f, 5500f, 3300f);

            Assert.That(downshift.ShouldShift, Is.True);
            Assert.That(downshift.RequestedGearIndex, Is.EqualTo(0));
            Assert.That(firstGear.ShouldShift, Is.False);
            Assert.That(firstGear.RequestedGearIndex, Is.EqualTo(0));
        }

        [Test]
        public void EvaluateGearboxTiming_BlocksGearSelectionDuringShiftAndCooldown()
        {
            var shift = VehicleDrivetrainRules.EvaluateGearboxTiming(0.1f, 0f, 0.1f, 0.4f);
            var cooldown = VehicleDrivetrainRules.EvaluateGearboxTiming(0f, 0.4f, 0.4f, 0.4f);
            var ready = VehicleDrivetrainRules.EvaluateGearboxTiming(0f, 0f, 0.4f, 0.4f);

            Assert.That(shift.CanEvaluateGearSelection, Is.False);
            Assert.That(shift.ShiftTimeRemaining, Is.EqualTo(0f));
            Assert.That(shift.ShiftCooldownRemaining, Is.EqualTo(0.4f));
            Assert.That(cooldown.CanEvaluateGearSelection, Is.False);
            Assert.That(cooldown.ShiftCooldownRemaining, Is.EqualTo(0f));
            Assert.That(ready.CanEvaluateGearSelection, Is.True);
        }
    }
}
