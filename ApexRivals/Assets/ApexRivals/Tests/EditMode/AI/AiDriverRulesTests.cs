using ApexRivals.AI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class AiDriverRulesTests
    {
        [Test]
        public void WrapWaypointIndex_EndOfClosedLine_ReturnsFirstIndex()
        {
            var index = AiDriverRules.WrapWaypointIndex(4, 4);

            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void CalculateSteering_TargetLeft_ReturnsNegative()
        {
            var steering = AiDriverRules.CalculateSteering(new Vector3(-3f, 0f, 10f), 1f);

            Assert.That(steering, Is.LessThan(0f));
        }

        [Test]
        public void CalculateSteering_TargetRight_ReturnsPositive()
        {
            var steering = AiDriverRules.CalculateSteering(new Vector3(3f, 0f, 10f), 1f);

            Assert.That(steering, Is.GreaterThan(0f));
        }

        [Test]
        public void CalculateSteering_ExtremeTarget_RemainsNormalized()
        {
            var steering = AiDriverRules.CalculateSteering(new Vector3(100f, 0f, 0.1f), 3f);

            Assert.That(steering, Is.InRange(-1f, 1f));
        }

        [Test]
        public void CreateDecision_StraightPathBelowTargetSpeed_RequestsHighThrottle()
        {
            var tuning = CreateTuning();

            var decision = AiDriverRules.CreateDecision(5f, 0f, 0f, tuning);

            Assert.That(decision.Throttle, Is.GreaterThan(0.8f));
            Assert.That(decision.Brake, Is.EqualTo(0f));
        }

        [Test]
        public void CreateDecision_SharpCorner_ReducesThrottle()
        {
            var tuning = CreateTuning();

            var straight = AiDriverRules.CreateDecision(4f, 0f, 0f, tuning);
            var corner = AiDriverRules.CreateDecision(4f, 0.8f, 1f, tuning);

            Assert.That(corner.Throttle, Is.LessThan(straight.Throttle));
        }

        [Test]
        public void CreateDecision_SharpCornerAboveTargetSpeed_RequestsBrake()
        {
            var tuning = CreateTuning();

            var decision = AiDriverRules.CreateDecision(12f, 0.8f, 1f, tuning);

            Assert.That(decision.Brake, Is.GreaterThan(0f));
        }

        [Test]
        public void CreateDecision_DriftBelowMinimumSpeed_IsDisabled()
        {
            var tuning = CreateTuning();

            var decision = AiDriverRules.CreateDecision(8f, 0.8f, 1f, tuning);

            Assert.That(decision.Drift, Is.False);
        }

        [Test]
        public void CreateDecision_DriftAboveCornerAndSpeedThresholds_IsEnabled()
        {
            var tuning = CreateTuning();

            var decision = AiDriverRules.CreateDecision(14f, 0.8f, 1f, tuning);

            Assert.That(decision.Drift, Is.True);
        }

        [Test]
        public void UpdateStuckTimer_BeforeDuration_DoesNotRequestRecovery()
        {
            var timer = AiDriverRules.UpdateStuckTimer(true, true, 0.2f, 1f, 1f, 0f);

            var shouldRecover = AiDriverRules.ShouldRequestRecovery(timer, 2f, 10f, 5f);

            Assert.That(shouldRecover, Is.False);
        }

        [Test]
        public void UpdateStuckTimer_AfterDuration_RequestsRecovery()
        {
            var timer = AiDriverRules.UpdateStuckTimer(true, true, 0.2f, 1f, 2.5f, 0f);

            var shouldRecover = AiDriverRules.ShouldRequestRecovery(timer, 2f, 10f, 5f);

            Assert.That(shouldRecover, Is.True);
        }

        [Test]
        public void ShouldRequestRecovery_DuringCooldown_DoesNotRequestRepeatedRecovery()
        {
            var shouldRecover = AiDriverRules.ShouldRequestRecovery(3f, 2f, 1f, 5f);

            Assert.That(shouldRecover, Is.False);
        }

        [Test]
        public void ApplyDifficulty_ChangesTuningValueWithoutMutatingBaseValue()
        {
            var baseTargetSpeed = 20f;

            var adjustedTargetSpeed = AiDriverRules.ApplyDifficulty(baseTargetSpeed, 1.25f);

            Assert.That(adjustedTargetSpeed, Is.EqualTo(25f));
            Assert.That(baseTargetSpeed, Is.EqualTo(20f));
        }

        private static AiDriverTuning CreateTuning()
        {
            return new AiDriverTuning(
                20f,
                5f,
                1f,
                1f,
                0.6f,
                10f,
                1f,
                2f,
                5f,
                8f,
                6f,
                10f);
        }
    }
}
