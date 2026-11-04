using ApexRivals.AI.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class AiPredictivePlannerTests
    {
        private static AiTacticalInput Input(float bend = 0f) => new AiTacticalInput(0f, 0f, 20f, 26f, bend, 0.9f, 3.5f, 5f, 0.4f);

        [Test]
        public void ClearRoad_RetainsUnrestrictedRacing()
        {
            var planner = new AiPredictivePlanner();
            var decision = planner.Decide(Input(), new AiTrafficCar[0]);
            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Racing));
            Assert.That(decision.LateralOffset, Is.Zero);
            Assert.That(decision.TrafficSpeedLimit, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void SlowCarWithFreeSide_PassesWithoutAnUnnecessarySpeedCap()
        {
            var planner = new AiPredictivePlanner();
            var decision = planner.Decide(Input(), new[] { new AiTrafficCar(30f, 0f, 8f) });
            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Overtaking));
            Assert.That(System.Math.Abs(decision.LateralOffset), Is.GreaterThanOrEqualTo(2.8f));
            Assert.That(decision.TrafficSpeedLimit, Is.EqualTo(float.PositiveInfinity));
            Assert.That(planner.FeasibleCandidates, Is.GreaterThan(0));
        }

        [Test]
        public void ClearingTraffic_ReleasesThePreviousPassingSide()
        {
            var planner = new AiPredictivePlanner();
            var first = planner.Decide(Input(), new[] { new AiTrafficCar(30f, 0f, 8f) });
            Assert.That(System.Math.Abs(first.LateralOffset), Is.GreaterThanOrEqualTo(2.8f));
            planner.Decide(Input(), new AiTrafficCar[0]);
            var next = planner.Decide(Input(), new[] { new AiTrafficCar(8f, 0f, 20f), new AiTrafficCar(0f, first.LateralOffset, 20f) });
            Assert.That(next.LateralOffset * first.LateralOffset, Is.LessThan(0f), "A new encounter must not inherit an obsolete passing side.");
            Assert.That(next.TrafficSpeedLimit, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void BlockedLanesAndImminentCollision_ChooseBraking()
        {
            var planner = new AiPredictivePlanner();
            var decision = planner.Decide(Input(), new[]
            {
                new AiTrafficCar(8f, 0f, 0f), new AiTrafficCar(0f, -3.5f, 20f), new AiTrafficCar(0f, 3.5f, 20f)
            });
            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Avoiding));
            Assert.That(decision.TrafficSpeedLimit, Is.Zero);
            Assert.That(planner.FeasibleCandidates, Is.Zero);
        }

        [Test]
        public void FastRivalApproachingOnRight_DoesNotCutAcrossItsTrajectory()
        {
            var planner = new AiPredictivePlanner();
            var decision = planner.Decide(Input(), new[] { new AiTrafficCar(24f, 0f, 10f), new AiTrafficCar(-12f, 3.5f, 38f) });
            Assert.That(decision.LateralOffset <= 0f || decision.TrafficSpeedLimit < 20f, Is.True);
        }

        [Test]
        public void RivalCrossingTheLine_IsPredictedBeforeItCurrentlyBlocksTheLane()
        {
            var staticDecision = new AiPredictivePlanner().Decide(Input(60f), new[] { new AiTrafficCar(8f, 4f, 15f) });
            var crossingDecision = new AiPredictivePlanner().Decide(Input(60f), new[] { new AiTrafficCar(8f, 4f, 15f, -3f) });
            Assert.That(staticDecision.TrafficSpeedLimit, Is.EqualTo(float.PositiveInfinity));
            Assert.That(crossingDecision.TrafficSpeedLimit, Is.LessThan(26f));
            Assert.That(crossingDecision.LateralOffset, Is.Zero, "Do not improvise a new passing lane through a sharp bend.");
        }
    }
}
