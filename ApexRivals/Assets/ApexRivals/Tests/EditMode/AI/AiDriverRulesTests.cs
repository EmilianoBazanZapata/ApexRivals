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
        public void UpdateStuckTimer_AccumulatesOnlyWhileExpectedToMoveAndSlow()
        {
            Assert.That(AiDriverRules.UpdateStuckTimer(true, true, 0.5f, 1.5f, 0.25f, 1f), Is.EqualTo(1.25f));
            Assert.That(AiDriverRules.UpdateStuckTimer(true, true, 5f, 1.5f, 0.25f, 1f), Is.EqualTo(0f), "Moving clears the timer.");
            Assert.That(AiDriverRules.UpdateStuckTimer(true, false, 0.5f, 1.5f, 0.25f, 1f), Is.EqualTo(0f), "Not trying to move is not stuck.");
            Assert.That(AiDriverRules.UpdateStuckTimer(false, true, 0.5f, 1.5f, 0.25f, 1f), Is.EqualTo(0f), "Outside a race nothing is stuck.");
        }

        [Test]
        public void ApplyDifficulty_ChangesTuningValueWithoutMutatingBaseValue()
        {
            var baseTargetSpeed = 20f;

            var adjustedTargetSpeed = AiDriverRules.ApplyDifficulty(baseTargetSpeed, 1.25f);

            Assert.That(adjustedTargetSpeed, Is.EqualTo(25f));
            Assert.That(baseTargetSpeed, Is.EqualTo(20f));
        }

        [Test]
        public void IdealDriver_TightCurveLookaheadNeverFallsBelowHardMinimum()
        {
            var lookahead = IdealRacingDriverRules.CalculateLookahead(20f, 20f, 1f, 10f, 18f);

            Assert.That(lookahead, Is.GreaterThanOrEqualTo(10f));
        }

        [Test]
        public void IdealDriver_TargetDistanceDoesNotChangeProfileSpeedOrThrottle()
        {
            const float currentSpeed = 8f;
            const float targetSpeed = 20f;
            var nearTargetCommand = IdealRacingDriverRules.CalculateSpeedCommand(currentSpeed, targetSpeed, 1.4f, 1.2f);
            var farTargetCommand = IdealRacingDriverRules.CalculateSpeedCommand(currentSpeed, targetSpeed, 1.4f, 1.2f);

            Assert.That(nearTargetCommand.Throttle, Is.EqualTo(farTargetCommand.Throttle));
            Assert.That(nearTargetCommand.Throttle, Is.GreaterThan(0f));
            Assert.That(nearTargetCommand.Brake, Is.Zero);
        }

        [Test]
        public void IdealDriver_StraightDrivingMaintainsThrottleFromSpeedProfile()
        {
            var straightTargetSpeed = IdealRacingDriverRules.CalculateProfileSpeed(20f, 6f, 0f, 18f, 1f);
            var speedCommand = IdealRacingDriverRules.CalculateSpeedCommand(8f, straightTargetSpeed, 1.4f, 1.2f);

            Assert.That(straightTargetSpeed, Is.EqualTo(20f));
            Assert.That(speedCommand.Throttle, Is.GreaterThan(0.5f));
            Assert.That(speedCommand.Brake, Is.Zero);
        }

        [Test]
        public void IdealDriver_ProjectionRightOfForwardPathHasPositiveCrossTrackError()
        {
            var root = CreateIdealLine(out var line,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f),
                new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, 0f));
            try
            {
                Assert.That(IdealRacingPath.TryBuild(line, 4f, 20f, 6f, 18f, out var path), Is.True);
                Assert.That(path.TryProjectLocal(new Vector3(2f, 0f, 8f), 0, 0, 0, out var projection), Is.True);
                Assert.That(projection.CrossTrackError, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdealDriver_HeadingPathToRightRequestsPositiveSteering()
        {
            var error = IdealRacingDriverRules.CalculateSignedHeadingError(Vector3.forward, new Vector3(1f, 0f, 1f));

            Assert.That(error, Is.GreaterThan(0f));
            Assert.That(IdealRacingDriverRules.CalculateSteering(error, 0f, 10f, 1f, 1f, 2f), Is.GreaterThan(0f));
        }

        [Test]
        public void IdealDriver_LocalProjectionDoesNotJumpAcrossParallelHairpin()
        {
            var root = CreateIdealLine(out var line,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f), new Vector3(3f, 0f, 20f),
                new Vector3(3f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(20f, 0f, 20f));
            try
            {
                Assert.That(IdealRacingPath.TryBuild(line, 4f, 20f, 6f, 18f, out var path), Is.True);
                Assert.That(path.TryProjectLocal(new Vector3(2.9f, 0f, 10f), 0, 0, 4, out var projection), Is.True);
                Assert.That(projection.SegmentIndex, Is.LessThan(5));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdealDriver_DensePathUsesTwoToFiveMetreSpacing()
        {
            var root = CreateIdealLine(out var line,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f),
                new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, 0f));
            try
            {
                Assert.That(IdealRacingPath.TryBuild(line, 4f, 20f, 6f, 18f, out var path), Is.True);
                Assert.That(path.SampleCount, Is.EqualTo(20));
                Assert.That(path.AverageSpacing, Is.InRange(2f, 5f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdealDriver_ArcLookaheadStaysLocalInsteadOfTakingCornerChord()
        {
            var root = CreateIdealLine(out var line,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f),
                new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, 0f));
            try
            {
                Assert.That(IdealRacingPath.TryBuild(line, 4f, 20f, 6f, 18f, out var path), Is.True);
                Assert.That(path.TryProjectLocal(Vector3.zero, 0, 0, 1, out var projection), Is.True);
                var target = path.GetPointAhead(projection, 8f, out var targetIndex, out var actualArcDistance);

                Assert.That(actualArcDistance, Is.EqualTo(8f).Within(0.001f));
                Assert.That(targetIndex, Is.LessThanOrEqualTo(1));
                Assert.That(target.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(target.z, Is.EqualTo(8f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdealDriver_CurvatureAndPreviewLowerSpeedBeforeCorner()
        {
            var straight = IdealRacingDriverRules.CalculateProfileSpeed(20f, 6f, 0f, 18f, 1f);
            var corner = IdealRacingDriverRules.CalculateProfileSpeed(20f, 6f, 0.12f, 18f, 1f);
            var command = IdealRacingDriverRules.CalculateSpeedCommand(14f, 7f, 1.4f, 1.2f);

            Assert.That(corner, Is.LessThan(straight));
            Assert.That(command.Throttle, Is.Zero);
            Assert.That(command.Brake, Is.GreaterThan(0f));
        }

        private static GameObject CreateIdealLine(out RacingLine racingLine, params Vector3[] points)
        {
            var root = new GameObject("IdealRacingLineTest");
            racingLine = root.AddComponent<RacingLine>();
            var waypoints = new Transform[points.Length];
            for (var index = 0; index < points.Length; index++)
            {
                var waypoint = new GameObject($"Waypoint_{index:000}").transform;
                waypoint.SetParent(root.transform);
                waypoint.position = points[index];
                waypoints[index] = waypoint;
            }

            racingLine.Configure(waypoints);
            return root;
        }

    }
}
