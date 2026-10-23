using ApexRivals.AI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class AiDriftRulesTests
    {
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
