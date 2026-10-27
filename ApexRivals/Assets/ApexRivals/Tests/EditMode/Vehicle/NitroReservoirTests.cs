using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class NitroReservoirTests
    {
        private static float Drift(NitroReservoir nitro, float dt) => nitro.Step(dt, true, true, 20f, 30f, 1f, 0f, false);

        [Test]
        public void StartsEmptyAndStraightDrivingNeverRefills()
        {
            var nitro = new NitroReservoir();
            nitro.Step(30f, true, true, 30f, 0f, 1f, 0f, false);
            Assert.That(nitro.Charge, Is.Zero);
            Assert.That(nitro.IsCharging, Is.False);
        }

        [Test]
        public void ShortSeparateSlidesCannotFarmFuel()
        {
            var nitro = new NitroReservoir();
            for (var i = 0; i < 20; i++)
            {
                Drift(nitro, 0.2f);
                nitro.Step(0.1f, true, true, 20f, 0f, 1f, 0f, false);
            }
            Assert.That(nitro.Charge, Is.Zero);
        }

        [Test]
        public void SustainedSlideChargesEquallyAtDifferentTimesteps()
        {
            var fine = new NitroReservoir();
            var coarse = new NitroReservoir();
            for (var i = 0; i < 200; i++) Drift(fine, 0.01f);
            for (var i = 0; i < 20; i++) Drift(coarse, 0.1f);
            Assert.That(fine.Charge, Is.GreaterThan(0f));
            Assert.That(fine.Charge, Is.EqualTo(coarse.Charge).Within(0.001f));
        }

        [TestCase(0f, 30f, true, true)]
        [TestCase(-20f, 30f, true, true)]
        [TestCase(20f, 80f, true, true)]
        [TestCase(20f, 30f, false, true)]
        [TestCase(20f, 30f, true, false)]
        public void InvalidMotionCannotEarnFuel(float speed, float angle, bool supported, bool allowed)
        {
            var nitro = new NitroReservoir();
            nitro.Step(10f, allowed, supported, speed, angle, 1f, 0f, false);
            Assert.That(nitro.Charge, Is.Zero);
        }

        [Test]
        public void ChargeIsCappedAndTwoSecondsConsumeAFullTank()
        {
            var nitro = new NitroReservoir();
            Drift(nitro, 20f);
            Assert.That(nitro.NormalizedCharge, Is.EqualTo(1f));
            Assert.That(nitro.Step(2f, true, true, 20f, 0f, 1f, 0f, true), Is.EqualTo(1f));
            Assert.That(nitro.Charge, Is.Zero);
            Assert.That(nitro.Step(0.02f, true, true, 20f, 0f, 1f, 0f, true), Is.Zero);
            Assert.That(nitro.IsBoosting, Is.False);
        }

        [Test]
        public void EvenHighQualityDriftingNeedsMoreThanEightSecondsToFill()
        {
            var nitro = new NitroReservoir();
            nitro.Step(8.35f, true, true, 20f, 40f, 1f, 0f, false);
            Assert.That(nitro.Charge, Is.EqualTo(96f).Within(0.001f));
            nitro.Step(0.4f, true, true, 20f, 40f, 1f, 0f, false);
            Assert.That(nitro.NormalizedCharge, Is.EqualTo(1f));
        }

        [Test]
        public void LastFuelFundsOnlyPartOfThePhysicsStep()
        {
            var nitro = new NitroReservoir();
            Drift(nitro, 0.5f);
            var fuel = nitro.Charge;
            Assert.That(nitro.Step(1f, true, true, 20f, 0f, 1f, 0f, true), Is.EqualTo(fuel / 50f).Within(0.0001f));
            Assert.That(nitro.Charge, Is.Zero);
        }

        [TestCase(false, true, 1f, 0f)]
        [TestCase(true, false, 1f, 0f)]
        [TestCase(true, true, 0f, 0f)]
        [TestCase(true, true, 1f, 1f)]
        public void PausingAirborneCoastingAndBrakingDoNotConsumeFuel(bool allowed, bool supported, float throttle, float brake)
        {
            var nitro = new NitroReservoir();
            Drift(nitro, 2f);
            var fuel = nitro.Charge;
            Assert.That(nitro.Step(1f, allowed, supported, 20f, 0f, throttle, brake, true), Is.Zero);
            Assert.That(nitro.Charge, Is.EqualTo(fuel));
        }

        [Test]
        public void CannotChargeAndBoostInTheSameStep()
        {
            var nitro = new NitroReservoir();
            Drift(nitro, 2f);
            var fuel = nitro.Charge;
            nitro.Step(0.1f, true, true, 20f, 30f, 1f, 0f, true);
            Assert.That(nitro.Charge, Is.EqualTo(fuel - 5f).Within(0.001f));
            Assert.That(nitro.IsCharging, Is.False);
            Assert.That(nitro.IsBoosting, Is.True);
        }

        [Test]
        public void ResetOrCollisionInterruptsQualificationWithoutRefillingTheTank()
        {
            var nitro = new NitroReservoir();
            Drift(nitro, 2f);
            var fuel = nitro.Charge;
            nitro.Interrupt();
            Drift(nitro, 0.2f);
            Assert.That(nitro.Charge, Is.EqualTo(fuel));
            Assert.That(nitro.IsCharging, Is.False);
        }

        [Test]
        public void InputRequestConsumptionPreservesNitroAndNeutralClearsIt()
        {
            var input = new DrivingInput(1f, 0f, 0.5f, true, true, RecoveryInputSource.KeyboardE, true);
            Assert.That(input.WithoutResetRequest().Nitro, Is.True);
            Assert.That(input.WithoutRecoveryRequest().Nitro, Is.True);
            Assert.That(DrivingInput.Neutral.Nitro, Is.False);
        }
    }
}
