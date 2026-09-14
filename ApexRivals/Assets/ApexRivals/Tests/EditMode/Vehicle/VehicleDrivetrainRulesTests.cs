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
