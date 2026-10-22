using System.Collections.Generic;
using ApexRivals.AI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class AiTacticalPlannerTests
    {
        private const float StraightSpeed = 40f;

        [Test]
        public void ClearTrack_RacesOnTheIdealLine()
        {
            var planner = new AiTacticalPlanner();

            var decision = planner.Decide(Input(speed: 30f, freeSpeed: StraightSpeed), new List<AiTrafficCar>());

            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Racing));
            Assert.That(decision.LateralOffset, Is.EqualTo(0f));
            Assert.That(decision.TrafficSpeedLimit, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void SlowerCarInACorner_IsFollowedNotPassed()
        {
            var planner = new AiTacticalPlanner();
            var cars = new List<AiTrafficCar> { new AiTrafficCar(20f, 0f, 15f) };

            var decision = planner.Decide(Input(speed: 18f, freeSpeed: 20f), cars);

            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Following));
            Assert.That(decision.TrafficSpeedLimit, Is.LessThan(20f));
        }

        [Test]
        public void SlowerCarOnAStraight_IsPassedOnTheFreeSide()
        {
            var planner = new AiTacticalPlanner();
            var cars = new List<AiTrafficCar> { new AiTrafficCar(20f, 0f, 30f) };

            var decision = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed), cars);

            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Overtaking));
            Assert.That(decision.LateralOffset, Is.Not.EqualTo(0f));
            Assert.That(planner.IsOvertaking, Is.True);
        }

        [Test]
        public void StoppedCarInACorner_IsDrivenAroundEvenByATimidDriver()
        {
            var planner = new AiTacticalPlanner();
            var cars = new List<AiTrafficCar> { new AiTrafficCar(25f, 0f, 0f) };

            var decision = planner.Decide(Input(speed: 12f, freeSpeed: 15f, aggression: 0.1f), cars);

            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Overtaking));
            Assert.That(decision.TrafficSpeedLimit, Is.GreaterThanOrEqualTo(AiRacecraftRules.PassingSpeed), "Never waits behind a stopped car.");
        }

        [Test]
        public void ImminentContactWithNoRoomToPass_SlowsBelowTheCarAhead()
        {
            var planner = new AiTacticalPlanner();
            var cars = new List<AiTrafficCar>
            {
                new AiTrafficCar(5f, 0f, 10f),
                new AiTrafficCar(0f, 3.2f, 20f),
                new AiTrafficCar(0f, -3.2f, 20f)
            };

            var decision = planner.Decide(Input(speed: 20f, freeSpeed: StraightSpeed), cars);

            Assert.That(decision.State, Is.EqualTo(AiDrivingState.Avoiding));
            Assert.That(decision.TrafficSpeedLimit, Is.LessThan(10f));
        }

        [Test]
        public void Overtake_HoldsThePassingLineUntilTheIdealLineIsClear()
        {
            var planner = new AiTacticalPlanner();
            var passOffset = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed), new List<AiTrafficCar> { new AiTrafficCar(20f, 0f, 30f) }).LateralOffset;

            // Now alongside: the passed car is just behind on the ideal line and no longer ahead in lane.
            var alongside = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed, crossTrack: passOffset, lateralOffset: passOffset), new List<AiTrafficCar> { new AiTrafficCar(-2f, 0f, 30f) });
            Assert.That(alongside.State, Is.EqualTo(AiDrivingState.Overtaking));
            Assert.That(alongside.LateralOffset, Is.EqualTo(passOffset));

            // Well clear: return to the ideal line.
            var clear = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed, crossTrack: passOffset, lateralOffset: passOffset), new List<AiTrafficCar> { new AiTrafficCar(-20f, 0f, 30f) });
            Assert.That(clear.State, Is.EqualTo(AiDrivingState.Racing));
            Assert.That(clear.LateralOffset, Is.EqualTo(0f));
            Assert.That(planner.IsOvertaking, Is.False);
        }

        [Test]
        public void Overtake_KeepsItsSideWhenTheCarAheadDriftsAcrossTheLine()
        {
            var planner = new AiTacticalPlanner();
            var first = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed), new List<AiTrafficCar> { new AiTrafficCar(20f, 0.5f, 30f) });
            Assert.That(first.State, Is.EqualTo(AiDrivingState.Overtaking));

            // The car ahead drifts to the other side of the ideal line: the shorter way round is now the opposite side.
            var next = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed, lateralOffset: first.LateralOffset), new List<AiTrafficCar> { new AiTrafficCar(16f, -0.5f, 30f) });

            Assert.That(next.State, Is.EqualTo(AiDrivingState.Overtaking));
            Assert.That(Mathf.Sign(next.LateralOffset), Is.EqualTo(Mathf.Sign(first.LateralOffset)), "Does not switch sides mid-pass.");
        }

        [Test]
        public void Overtake_SwitchesSideOnlyWhenTheCommittedSideCloses()
        {
            var planner = new AiTacticalPlanner();
            var first = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed), new List<AiTrafficCar> { new AiTrafficCar(20f, 0.5f, 30f) });
            var side = Mathf.Sign(first.LateralOffset);

            // A third car fills the committed side alongside.
            var cars = new List<AiTrafficCar> { new AiTrafficCar(16f, 0.5f, 30f), new AiTrafficCar(0f, side * 3.2f, 36f) };
            var next = planner.Decide(Input(speed: 36f, freeSpeed: StraightSpeed, lateralOffset: first.LateralOffset), cars);

            Assert.That(Mathf.Sign(next.LateralOffset), Is.EqualTo(-side));
        }

        [Test]
        public void FasterCarCloseBehind_IsDefendedOnlyByAggressiveDrivers()
        {
            var attacker = new List<AiTrafficCar> { new AiTrafficCar(-5f, 2f, 35f) };

            var aggressive = new AiTacticalPlanner().Decide(Input(speed: 30f, freeSpeed: StraightSpeed, aggression: 0.9f), attacker);
            var timid = new AiTacticalPlanner().Decide(Input(speed: 30f, freeSpeed: StraightSpeed, aggression: 0.3f), attacker);

            Assert.That(aggressive.State, Is.EqualTo(AiDrivingState.Defending));
            Assert.That(aggressive.LateralOffset, Is.GreaterThan(0f), "Moves toward the attacker's side.");
            Assert.That(timid.State, Is.EqualTo(AiDrivingState.Racing));
        }

        private static AiTacticalInput Input(float speed, float freeSpeed, float aggression = 0.6f, float crossTrack = 0f, float lateralOffset = 0f)
        {
            return new AiTacticalInput(crossTrack, lateralOffset, speed, freeSpeed, StraightSpeed, aggression, 3.5f, 5f, 0.6f);
        }
    }
}
