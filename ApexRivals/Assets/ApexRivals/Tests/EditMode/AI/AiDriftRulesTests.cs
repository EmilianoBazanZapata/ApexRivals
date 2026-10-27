using ApexRivals.AI.Runtime;
using ApexRivals.AI.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class AiDriftRulesTests
    {
        [Test]
        public void SustainedDrift_FollowsCornerRotationButCatchesExcessYaw()
        {
            var root = new GameObject("SustainedCornerTest");
            var configuration = ScriptableObject.CreateInstance<AiDriverConfiguration>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"targetSpeed\":40,\"minimumCornerSpeed\":14,\"curvatureSpeedScale\":0,\"corneringGrip\":0,\"driftAffinity\":1}", configuration);
                var positions = new[] { Vector3.zero, new Vector3(0f, 0f, 80f), new Vector3(80f, 0f, 80f), new Vector3(80f, 0f, 0f) };
                var points = new Transform[positions.Length];
                for (var index = 0; index < points.Length; index++)
                {
                    points[index] = new GameObject("Waypoint").transform;
                    points[index].SetParent(root.transform);
                    points[index].position = positions[index];
                }

                var line = root.AddComponent<RacingLine>();
                line.Configure(points);
                var tracker = new AiPathTracker();
                var position = new Vector3(0f, 0f, 64f);
                Assert.That(tracker.TryBuild(line, 4f, configuration, position, null), Is.True);
                Assert.That(tracker.TryProject(position, 4, 4), Is.True);
                var drift = new AiDriftController();
                drift.Update(tracker.Path, tracker.Projection, 15f, 15f, 0f, 0f, true, configuration, 0.02f);
                var lateralSpeed = -15f * Mathf.Tan(configuration.DriftTargetSlipAngle * Mathf.Deg2Rad);
                drift.Update(tracker.Path, tracker.Projection, 15f, 15f, lateralSpeed, 45f, true, configuration, 0.02f);
                drift.Update(tracker.Path, tracker.Projection, 15f, 15f, lateralSpeed, 45f, true, configuration, 0.02f);
                Assert.That(drift.Phase, Is.EqualTo(AiDriftPhase.Sustain));
                Assert.That(drift.SteeringCorrection, Is.EqualTo(0f).Within(0.001f), "Holding the angle through a corner must not damp the necessary road rotation to zero.");
                drift.Update(tracker.Path, tracker.Projection, 15f, 15f, lateralSpeed, 90f, true, configuration, 0.02f);
                Assert.That(drift.SteeringCorrection, Is.LessThan(0f), "Excess rotation must still trigger countersteer.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(configuration);
            }
        }

        [TestCase(false, 15f)]
        [TestCase(true, 15f)]
        [TestCase(false, 20f)]
        [TestCase(true, 20f)]
        public void DriftControl_KeepsPowerInClearCornerButBrakesForTrafficOrProps(bool prop, float entrySpeed)
        {
            var root = new GameObject("DriftControlTest");
            var configuration = ScriptableObject.CreateInstance<AiDriverConfiguration>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"targetSpeed\":40,\"minimumCornerSpeed\":14,\"curvatureSpeedScale\":0,\"corneringGrip\":0,\"driftAffinity\":1,\"steeringSmoothing\":0,\"throttleSmoothing\":6,\"brakeSmoothing\":10}", configuration);
                var line = root.AddComponent<RacingLine>();
                var points = new[] { Vector3.zero, new Vector3(0f, 0f, 80f), new Vector3(80f, 0f, 80f), new Vector3(80f, 0f, 0f) };
                var waypoints = new Transform[points.Length];
                for (var index = 0; index < points.Length; index++)
                {
                    waypoints[index] = new GameObject("Waypoint").transform;
                    waypoints[index].SetParent(root.transform);
                    waypoints[index].position = points[index];
                }

                line.Configure(waypoints);
                var vehicle = new GameObject("Driver").transform;
                vehicle.SetParent(root.transform);
                vehicle.position = new Vector3(0f, 0f, 64f);
                var tracker = new AiPathTracker();
                Assert.That(tracker.TryBuild(line, 4f, configuration, vehicle.position, null), Is.True);
                Assert.That(tracker.TryProject(vehicle.position, 4, 4), Is.True);
                var drift = new AiDriftController();
                drift.Update(tracker.Path, tracker.Projection, entrySpeed, entrySpeed, 0f, 0f, true, configuration, 0.02f);
                Assert.That(drift.IsDrifting, Is.True, "The corner must initiate a real drift.");
                var control = new AiVehicleControl();
                var settings = new AiControlSettings(2f, 4.5f, 0.75f, 7f, 9f, 0.6f);
                var personality = new AiDriverPersonality();
                var tuning = configuration.CreateTuning();
                control.Drive(tracker, vehicle, entrySpeed, 0f, 0f, float.PositiveInfinity, float.PositiveInfinity, false, personality, drift, configuration, tuning, settings, 0.02f);
                Assert.That(control.Input.Drift, Is.True);
                Assert.That(control.ThrottleCommand, Is.EqualTo(1f));
                Assert.That(control.BrakeCommand, Is.Zero);

                control.Drive(tracker, vehicle, entrySpeed, 0f, 0f, prop ? float.PositiveInfinity : 5f, prop ? 5f : float.PositiveInfinity, false, personality, drift, configuration, tuning, settings, 0.02f);
                Assert.That(control.ThrottleCommand, Is.Zero);
                Assert.That(control.BrakeCommand, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.75f));
                Assert.That(control.Input.Drift, Is.False, "Emergency braking must release the handbrake kick.");
                Assert.That(control.Input.Throttle, Is.Zero, "Residual throttle must not fight the brake.");
                control.Drive(tracker, vehicle, entrySpeed, 0f, 0f, float.PositiveInfinity, float.PositiveInfinity, false, personality, drift, configuration, tuning, settings, 0.02f);
                Assert.That(control.Input.Brake, Is.Zero, "Release the normal brake immediately when returning to drift power.");
                JsonUtility.FromJsonOverwrite("{\"cornerSpeedFactor\":0.25}", configuration);
                control.Drive(tracker, vehicle, entrySpeed, 0f, 0f, float.PositiveInfinity, float.PositiveInfinity, false, personality, drift, configuration, tuning, settings, 0.02f);
                Assert.That(control.ThrottleCommand, Is.Zero, "A tightening corner must stop drift acceleration before leaving the road.");
                Assert.That(control.Input.Brake, Is.GreaterThan(0f));
                Assert.That(control.Input.Drift, Is.False, "Normal braking must release the handbrake.");
                drift.Update(tracker.Path, tracker.Projection, entrySpeed, entrySpeed, 0f, 0f, true, configuration, 0.61f);
                Assert.That(drift.ButtonPressed, Is.False, "The handbrake pulse must always have a bounded duration.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void ExitThrottle_ExpertRecoversPowerAsTheCarSettles()
        {
            Assert.That(AiDriftRules.CalculateExitThrottle(5f, 15f, 1f), Is.EqualTo(1f));
            Assert.That(AiDriftRules.CalculateExitThrottle(25f, 15f, 1f), Is.EqualTo(0.5f));
            Assert.That(AiDriftRules.CalculateExitThrottle(5f, 90f, 1f), Is.EqualTo(0.5f));
            Assert.That(AiDriftRules.CalculateExitThrottle(15f, 45f, 1f), Is.GreaterThan(0.5f).And.LessThan(1f));
            Assert.That(AiDriftRules.CalculateExitThrottle(-15f, -45f, 1f), Is.EqualTo(AiDriftRules.CalculateExitThrottle(15f, 45f, 1f)));
            Assert.That(AiDriftRules.CalculateExitThrottle(5f, 15f, 0.5f), Is.LessThan(AiDriftRules.CalculateExitThrottle(5f, 15f, 1f)));
        }

        [Test]
        public void MinimumCornerAngle_FallsWithAffinityAndNeverDriftsAtZero()
        {
            Assert.That(AiDriftRules.CalculateMinimumCornerAngle(0f), Is.EqualTo(float.PositiveInfinity));
            Assert.That(AiDriftRules.CalculateMinimumCornerAngle(1f), Is.EqualTo(AiDriftRules.MostEagerCornerAngle));
            Assert.That(AiDriftRules.CalculateMinimumCornerAngle(0.2f), Is.GreaterThan(AiDriftRules.CalculateMinimumCornerAngle(0.6f)));
        }

        [Test]
        public void DriftCorner_CountsLeftAndRightTurns()
        {
            Assert.That(AiDriftRules.IsDriftCorner(-60f, 45f), Is.True);
            Assert.That(AiDriftRules.IsDriftCorner(60f, 45f), Is.True);
            Assert.That(AiDriftRules.IsDriftCorner(30f, 45f), Is.False);
            Assert.That(AiDriftRules.IsDriftCorner(90f, 45f, 15f), Is.True);
            Assert.That(AiDriftRules.IsDriftCorner(90f, 45f, 8f), Is.False, "A crawl-speed hairpin is braked for.");
        }

        [Test]
        public void DriftThrottle_PowersBelowTargetAndLiftsTowardMaximum()
        {
            Assert.That(AiDriftRules.CalculateDriftThrottle(10f, 25f, 55f, 0.35f), Is.EqualTo(1f));
            var midway = AiDriftRules.CalculateDriftThrottle(40f, 25f, 55f, 0.35f);
            Assert.That(midway, Is.LessThan(1f).And.GreaterThan(0.35f));
            Assert.That(AiDriftRules.CalculateDriftThrottle(80f, 25f, 55f, 0.35f), Is.EqualTo(0.35f), "Keeps enough power to stay in a forward drift.");
        }

        [Test]
        public void SlipIntoCorner_IsPositiveWhenTheNosePointsIntoTheTurn()
        {
            Assert.That(AiDriftRules.CalculateSlipIntoCorner(-20f, 90f), Is.EqualTo(20f), "Right-hander, sliding left.");
            Assert.That(AiDriftRules.CalculateSlipIntoCorner(20f, -90f), Is.EqualTo(20f), "Left-hander, sliding right.");
            Assert.That(AiDriftRules.CalculateSlipIntoCorner(20f, 90f), Is.EqualTo(-20f), "Wrong way.");
        }

        [Test]
        public void Kick_EndsOnceTheTailIsOnItsWay()
        {
            Assert.That(AiDriftRules.ShouldEndKick(2f, 25f, 10f, 0.1f, 0.35f), Is.False, "Tail not moving yet.");
            Assert.That(AiDriftRules.ShouldEndKick(16f, 25f, 10f, 0.1f, 0.35f), Is.True, "Slide building.");
            Assert.That(AiDriftRules.ShouldEndKick(2f, 25f, 55f, 0.1f, 0.35f), Is.True, "Already rotating fast.");
            Assert.That(AiDriftRules.ShouldEndKick(2f, 25f, 10f, 0.4f, 0.35f), Is.True, "Never held long enough to lock the rear.");
        }

        [Test]
        public void KickAgain_OnlyWhenTheSlideHasDied()
        {
            Assert.That(AiDriftRules.ShouldKickAgain(3f, 25f, 5f, 0.6f), Is.True);
            Assert.That(AiDriftRules.ShouldKickAgain(20f, 25f, 5f, 0.6f), Is.False, "Slide alive.");
            Assert.That(AiDriftRules.ShouldKickAgain(3f, 25f, 40f, 0.6f), Is.False, "Still rotating in.");
            Assert.That(AiDriftRules.ShouldKickAgain(3f, 25f, 5f, 0.1f), Is.False, "Just released.");
        }

        [Test]
        public void SlideSteering_CountersteersOverRotationAndTurnsInWhenShort()
        {
            Assert.That(AiDriftRules.CalculateSlideSteering(25f, 25f, 0f, 1f), Is.EqualTo(0f), "On target and steady.");
            Assert.That(AiDriftRules.CalculateSlideSteering(40f, 25f, 0f, 1f), Is.LessThan(0f), "Too much angle in a right-hander: steer left.");
            Assert.That(AiDriftRules.CalculateSlideSteering(25f, 25f, 60f, 1f), Is.LessThan(0f), "Still rotating in: catch it early.");
            Assert.That(AiDriftRules.CalculateSlideSteering(5f, 25f, 0f, 1f), Is.GreaterThan(0f), "Too little: turn in.");
            Assert.That(AiDriftRules.CalculateSlideSteering(40f, 25f, 0f, -1f), Is.GreaterThan(0f), "Mirrored for a left-hander.");
        }

        [Test]
        public void DriftSkill_PerfectDrifterIsExactAndNeverBotches()
        {
            Assert.That(AiDriftRules.CalculateMistakeChance(1f), Is.EqualTo(0f));
            Assert.That(AiDriftRules.CalculateSkillGainScale(1f), Is.EqualTo(1f));
            Assert.That(AiDriftRules.CalculateDriftSlipTarget(25f, 1f, 0.9f), Is.EqualTo(25f));
        }

        [Test]
        public void DriftSkill_LessSkilledDrifterVariesCorrectsSofterAndBotchesSome()
        {
            Assert.That(AiDriftRules.CalculateMistakeChance(0.7f), Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(AiDriftRules.CalculateSkillGainScale(0.7f), Is.LessThan(1f));
            Assert.That(AiDriftRules.CalculateDriftSlipTarget(22f, 0.7f, 1f), Is.EqualTo(28f).Within(0.001f));
            Assert.That(AiDriftRules.CalculateDriftSlipTarget(22f, 0.7f, -1f), Is.EqualTo(16f).Within(0.001f));
        }

        [Test]
        public void SlideSteering_SoftenedByLowerReactionGain()
        {
            var sharp = AiDriftRules.CalculateSlideSteering(40f, 25f, 30f, 1f, 1f);
            var soft = AiDriftRules.CalculateSlideSteering(40f, 25f, 30f, 1f, 0.85f);
            Assert.That(Mathf.Abs(soft), Is.LessThan(Mathf.Abs(sharp)));
        }

        [Test]
        public void Straight_WhenPointingWhereItGoesWithoutRotating()
        {
            Assert.That(AiDriftRules.IsStraight(3f, 10f), Is.True);
            Assert.That(AiDriftRules.IsStraight(3f, 60f), Is.False);
            Assert.That(AiDriftRules.IsStraight(20f, 10f), Is.False);
        }

        [Test]
        public void ExitDrift_WhenCornerOpensSpinsSlowsOrRunsLong()
        {
            Assert.That(AiDriftRules.ShouldExitDrift(60f, 20f, -20f, 55f, 20f, 7f, 1f, 3.5f), Is.True, "Sliding the wrong way.");
            Assert.That(AiDriftRules.ShouldExitDrift(60f, 20f, 25f, 55f, 20f, 7f, 1f, 3.5f), Is.False, "Mid-corner, holding the slide.");
            Assert.That(AiDriftRules.ShouldExitDrift(10f, 20f, 25f, 55f, 20f, 7f, 1f, 3.5f), Is.True, "Corner done.");
            Assert.That(AiDriftRules.ShouldExitDrift(60f, 20f, 70f, 55f, 20f, 7f, 1f, 3.5f), Is.True, "About to spin.");
            Assert.That(AiDriftRules.ShouldExitDrift(60f, 20f, 25f, 55f, 5f, 7f, 1f, 3.5f), Is.True, "Too slow.");
            Assert.That(AiDriftRules.ShouldExitDrift(60f, 20f, 25f, 55f, 20f, 7f, 4f, 3.5f), Is.True, "Too long.");
        }

        [Test]
        public void SlipAngle_IsSignedBySlideDirection()
        {
            Assert.That(AiDriftRules.CalculateSlipAngle(20f, 0f), Is.EqualTo(0f));
            Assert.That(AiDriftRules.CalculateSlipAngle(10f, 10f), Is.EqualTo(45f).Within(0.01f));
            Assert.That(AiDriftRules.CalculateSlipAngle(10f, -10f), Is.EqualTo(-45f).Within(0.01f));
        }

        [Test]
        public void HeadingChange_MeasuresTheCornerAheadOnly()
        {
            var root = new GameObject("DriftLine");
            try
            {
                // Long straight north, then a right-hand hairpin and a straight back south.
                var racingLine = root.AddComponent<RacingLine>();
                var points = new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 80f), new Vector3(0f, 0f, 120f),
                    new Vector3(10f, 0f, 130f), new Vector3(20f, 0f, 120f), new Vector3(20f, 0f, 0f), new Vector3(10f, 0f, -10f)
                };
                var waypoints = new Transform[points.Length];
                for (var index = 0; index < points.Length; index++)
                {
                    waypoints[index] = new GameObject($"Waypoint_{index}").transform;
                    waypoints[index].SetParent(root.transform);
                    waypoints[index].position = points[index];
                }

                racingLine.Configure(waypoints);
                Assert.That(IdealRacingPath.TryBuild(racingLine, 4f, 40f, 8f, 18f, out var path), Is.True);
                var start = new Vector3(0f, 0f, 20f);
                Assert.That(path.TryProjectLocal(start, path.FindNearestSegment(start), 4, 4, out var projection), Is.True);

                Assert.That(Mathf.Abs(path.GetHeadingChange(projection, 0f, 40f)), Is.LessThan(20f), "Straight ahead.");
                Assert.That(path.GetHeadingChange(projection, 70f, 150f), Is.GreaterThan(120f), "The hairpin turns right.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
