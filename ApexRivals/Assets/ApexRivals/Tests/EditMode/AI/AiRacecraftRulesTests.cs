using System.Collections.Generic;
using ApexRivals.AI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class AiRacecraftRulesTests
    {
        [Test]
        public void Assess_PicksNearestCarAheadInLaneAndIgnoresCarsInOtherLanes()
        {
            var cars = new List<AiTrafficCar>
            {
                new AiTrafficCar(30f, 0.5f, 20f),
                new AiTrafficCar(12f, -0.3f, 18f),
                new AiTrafficCar(8f, 5f, 10f)
            };

            var traffic = AiRacecraftRules.Assess(0f, 25f, cars);

            Assert.That(traffic.HasCarAhead, Is.True);
            Assert.That(traffic.AheadGap, Is.EqualTo(12f));
            Assert.That(traffic.AheadSpeed, Is.EqualTo(18f));
        }

        [Test]
        public void Assess_CarAlongsideBlocksThatSideOnly()
        {
            var cars = new List<AiTrafficCar> { new AiTrafficCar(1f, 3f, 20f) };

            var traffic = AiRacecraftRules.Assess(0f, 20f, cars);

            Assert.That(traffic.RightBlocked, Is.True);
            Assert.That(traffic.LeftBlocked, Is.False);
            Assert.That(traffic.HasCarAhead, Is.False);
        }

        [Test]
        public void Assess_FasterCarCloseBehindIsAnAttacker()
        {
            var cars = new List<AiTrafficCar> { new AiTrafficCar(-5f, 1f, 30f) };

            var traffic = AiRacecraftRules.Assess(0f, 25f, cars);

            Assert.That(traffic.HasAttacker, Is.True);
            Assert.That(traffic.AttackerLateral, Is.EqualTo(1f));
        }

        [Test]
        public void FollowSpeed_SlowsBelowLeaderWhenTooCloseAndAllowsClosingWhenFar()
        {
            var tooClose = AiRacecraftRules.CalculateFollowSpeed(5f, 20f, 25f, 0.6f);
            var farBehind = AiRacecraftRules.CalculateFollowSpeed(40f, 20f, 25f, 0.6f);

            Assert.That(tooClose, Is.LessThan(20f));
            Assert.That(farBehind, Is.GreaterThan(20f));
        }

        [Test]
        public void ShouldAttemptOvertake_AggressiveDriversNeedLessSpeedAdvantage()
        {
            Assert.That(AiRacecraftRules.ShouldAttemptOvertake(22f, 20f, 15f, 0.9f), Is.True);
            Assert.That(AiRacecraftRules.ShouldAttemptOvertake(22f, 20f, 15f, 0.1f), Is.False);
            Assert.That(AiRacecraftRules.ShouldAttemptOvertake(30f, 20f, 40f, 1f), Is.False, "Too far away to commit to a pass.");
        }

        [Test]
        public void PassingZone_IsNearStraightSpeedOnly()
        {
            Assert.That(AiRacecraftRules.IsPassingZone(42f, 48f), Is.True);
            Assert.That(AiRacecraftRules.IsPassingZone(20f, 48f), Is.False, "Corners are for following.");
        }

        [Test]
        public void TryChooseOvertakeOffset_PrefersSmallerDeviationAndAvoidsBlockedSide()
        {
            Assert.That(AiRacecraftRules.TryChooseOvertakeOffset(1f, false, false, 3f, 5f, out var free), Is.True);
            Assert.That(free, Is.EqualTo(-2f), "Passing left of a car right of the line deviates less.");

            Assert.That(AiRacecraftRules.TryChooseOvertakeOffset(1f, true, false, 3f, 5f, out var rightOnly), Is.True);
            Assert.That(rightOnly, Is.EqualTo(4f));

            Assert.That(AiRacecraftRules.TryChooseOvertakeOffset(1f, true, true, 3f, 5f, out _), Is.False);
        }

        [Test]
        public void TryChooseOvertakeOffset_RejectsPassesBeyondTheTrackWidth()
        {
            Assert.That(AiRacecraftRules.TryChooseOvertakeOffset(3f, true, false, 3f, 4f, out _), Is.False);
        }

        [Test]
        public void DefendOffset_OnlyAggressiveDriversCoverTheAttackerWithinLimits()
        {
            Assert.That(AiRacecraftRules.CalculateDefendOffset(0f, 3f, 0.3f, 4f), Is.EqualTo(0f));
            Assert.That(AiRacecraftRules.CalculateDefendOffset(0f, 3f, 0.9f, 4f), Is.EqualTo(1.5f));
            Assert.That(AiRacecraftRules.CalculateDefendOffset(3.5f, 6f, 0.9f, 4f), Is.EqualTo(4f));
        }

        [Test]
        public void IsIdealLineClear_FalseWhileAPassedCarIsStillOnTheLineNearby()
        {
            Assert.That(AiRacecraftRules.IsIdealLineClear(new List<AiTrafficCar> { new AiTrafficCar(-3f, 0.5f, 20f) }, 6f, 10f), Is.False);
            Assert.That(AiRacecraftRules.IsIdealLineClear(new List<AiTrafficCar> { new AiTrafficCar(-9f, 0.5f, 20f) }, 6f, 10f), Is.True);
            Assert.That(AiRacecraftRules.IsIdealLineClear(new List<AiTrafficCar> { new AiTrafficCar(-3f, 4f, 20f) }, 6f, 10f), Is.True);
        }

        [Test]
        public void ObstacleSpeedLimit_AllowsStoppingBeforeTheMargin()
        {
            Assert.That(AiRacecraftRules.CalculateObstacleSpeedLimit(3f, 3f, 10f), Is.EqualTo(0f));
            Assert.That(AiRacecraftRules.CalculateObstacleSpeedLimit(23f, 3f, 10f), Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void WrapLapGap_UsesTheShorterDirectionAcrossTheFinishLine()
        {
            Assert.That(AiRacecraftRules.WrapLapGap(5f, 995f, 1000f), Is.EqualTo(10f).Within(0.001f));
            Assert.That(AiRacecraftRules.WrapLapGap(995f, 5f, 1000f), Is.EqualTo(-10f).Within(0.001f));
        }

        [Test]
        public void RecoveryAction_FlippedCarResetsImmediately()
        {
            var action = AiRecoveryRules.ResolveRecoveryAction(true, 0f, 2f, false, 0f, 5f, out var reason);

            Assert.That(action, Is.EqualTo(AiRecoveryAction.Reset));
            Assert.That(reason, Is.EqualTo(AiResetReason.UpsideDown));
        }

        [Test]
        public void RecoveryAction_StuckUprightCarReversesFirstThenResetsIfStillStuck()
        {
            var first = AiRecoveryRules.ResolveRecoveryAction(false, 3f, 2f, false, 0f, 5f, out var firstReason);
            var second = AiRecoveryRules.ResolveRecoveryAction(false, 3f, 2f, true, 0f, 5f, out var secondReason);

            Assert.That(first, Is.EqualTo(AiRecoveryAction.Reverse));
            Assert.That(firstReason, Is.EqualTo(AiResetReason.StuckNoProgress));
            Assert.That(second, Is.EqualTo(AiRecoveryAction.Reset), "C02: an upright stuck car must eventually reset.");
            Assert.That(secondReason, Is.EqualTo(AiResetReason.RecoveryFailed));
        }

        [Test]
        public void RecoveryAction_LongOffTrackResetsAndShortExcursionDoesNot()
        {
            Assert.That(AiRecoveryRules.ResolveRecoveryAction(false, 0f, 2f, false, 6f, 5f, out var reason), Is.EqualTo(AiRecoveryAction.Reset));
            Assert.That(reason, Is.EqualTo(AiResetReason.OffTrack));
            Assert.That(AiRecoveryRules.ResolveRecoveryAction(false, 0f, 2f, false, 2f, 5f, out _), Is.EqualTo(AiRecoveryAction.None));
        }

        [Test]
        public void ResetAdvance_MovesStuckCarsPastTheObstacleAndFurtherOnRepeats()
        {
            var flipped = AiRecoveryRules.CalculateResetAdvance(AiResetReason.UpsideDown, 0);
            var stuck = AiRecoveryRules.CalculateResetAdvance(AiResetReason.RecoveryFailed, 0);
            var stuckAgain = AiRecoveryRules.CalculateResetAdvance(AiResetReason.RecoveryFailed, 1);

            Assert.That(stuck, Is.GreaterThan(flipped));
            Assert.That(stuckAgain, Is.GreaterThan(stuck));
            Assert.That(AiRecoveryRules.CalculateResetAdvance(AiResetReason.RecoveryFailed, 50), Is.EqualTo(60f));
        }

        [Test]
        public void ResetAdvance_NeverJumpsOverTheNextCheckpoint()
        {
            Assert.That(AiRecoveryRules.ClampResetAdvanceToCheckpoint(25f, 12f, 5f), Is.EqualTo(7f));
            Assert.That(AiRecoveryRules.ClampResetAdvanceToCheckpoint(25f, 3f, 5f), Is.EqualTo(0f));
            Assert.That(AiRecoveryRules.ClampResetAdvanceToCheckpoint(25f, 200f, 5f), Is.EqualTo(25f));
            Assert.That(AiRecoveryRules.ClampResetAdvanceToCheckpoint(25f, -1f, 5f), Is.EqualTo(25f), "Unknown checkpoint gap leaves the advance alone.");
        }

        [Test]
        public void CheckpointLeftBehind_IsMissedOnlyWhenClearlyPastIt()
        {
            const float lap = 3600f;

            Assert.That(AiRecoveryRules.IsCheckpointMissed(lap - 40f, lap, 20f, 200f), Is.True, "Went round it 40 m ago.");
            Assert.That(AiRecoveryRules.IsCheckpointMissed(lap - 5f, lap, 20f, 200f), Is.False, "Still crossing the trigger.");
            Assert.That(AiRecoveryRules.IsCheckpointMissed(120f, lap, 20f, 200f), Is.False, "Ahead, as usual.");
            Assert.That(AiRecoveryRules.IsCheckpointMissed(lap - 900f, lap, 20f, 200f), Is.False, "Too far back to be a miss.");
            Assert.That(AiRecoveryRules.IsCheckpointMissed(-1f, lap, 20f, 200f), Is.False, "Unknown or race finished.");
        }

        [Test]
        public void MissedCheckpointReset_LandsJustBeforeTheCheckpoint()
        {
            const float lap = 3600f;
            var gap = lap - 40f;

            var advance = AiRecoveryRules.CalculateMissedCheckpointResetAdvance(gap, 10f);

            Assert.That(lap - advance, Is.EqualTo(50f), "Ends up 10 m before a checkpoint that was 40 m behind.");
        }

        [Test]
        public void ResetLateralOffset_TriesCentreThenAlternatesWithinTrackWidth()
        {
            Assert.That(AiRecoveryRules.CalculateResetLateralOffset(0, 4f), Is.EqualTo(0f));
            Assert.That(AiRecoveryRules.CalculateResetLateralOffset(1, 4f), Is.EqualTo(3f));
            Assert.That(AiRecoveryRules.CalculateResetLateralOffset(2, 4f), Is.EqualTo(-3f));
            Assert.That(AiRecoveryRules.CalculateResetLateralOffset(3, 4f), Is.EqualTo(4f));
            Assert.That(AiRecoveryRules.CalculateResetLateralOffset(4, 4f), Is.EqualTo(-4f));
        }

        [Test]
        public void OffTrackTimer_AccumulatesOffTrackAndClearsOnTrack()
        {
            var timer = AiRecoveryRules.UpdateOffTrackTimer(12f, 10f, 0.5f, 1f);
            Assert.That(timer, Is.EqualTo(1.5f));
            Assert.That(AiRecoveryRules.UpdateOffTrackTimer(4f, 10f, 0.5f, timer), Is.EqualTo(0f));
        }

        [Test]
        public void LowSpeedSteering_IsCappedWhenSlowAndFreeAtSpeed()
        {
            Assert.That(AiDriverRules.LimitLowSpeedSteering(1f, 2f), Is.EqualTo(0.5f));
            Assert.That(AiDriverRules.LimitLowSpeedSteering(-1f, 2f), Is.EqualTo(-0.5f));
            Assert.That(AiDriverRules.LimitLowSpeedSteering(1f, 20f), Is.EqualTo(1f));
            Assert.That(AiDriverRules.LimitLowSpeedSteering(0.3f, 2f), Is.EqualTo(0.3f));
        }

        [Test]
        public void BrakeBlend_KeepsBrakingStraightAndEasesItInHardTurns()
        {
            Assert.That(AiDriverRules.BlendBrakeWithSteering(1f, 0f), Is.EqualTo(1f));
            Assert.That(AiDriverRules.BlendBrakeWithSteering(1f, 0.4f), Is.EqualTo(1f));
            Assert.That(AiDriverRules.BlendBrakeWithSteering(1f, 1f), Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void SlideInputScale_EasesInputsOnlyWhileSliding()
        {
            Assert.That(AiDriverRules.CalculateSlideInputScale(1f), Is.EqualTo(1f));
            Assert.That(AiDriverRules.CalculateSlideInputScale(-8f), Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void RejoinLookahead_GrowsWithDistanceFromTheLine()
        {
            Assert.That(AiDriverRules.CalculateRejoinLookahead(2f, 7f), Is.EqualTo(7f));
            Assert.That(AiDriverRules.CalculateRejoinLookahead(10f, 7f), Is.EqualTo(20f));
            Assert.That(AiDriverRules.CalculateRejoinLookahead(40f, 7f), Is.EqualTo(25f));
        }

        [Test]
        public void LaunchThrottle_IsCappedFromStandstillAndFreeAtSpeed()
        {
            Assert.That(AiDriverRules.LimitLaunchThrottle(1f, 0f), Is.EqualTo(0.45f));
            Assert.That(AiDriverRules.LimitLaunchThrottle(1f, 10f), Is.EqualTo(1f));
            Assert.That(AiDriverRules.LimitLaunchThrottle(0.2f, 0f), Is.EqualTo(0.2f));
        }

        [Test]
        public void ReverseSteering_TurnsOppositeToTheHeadingError()
        {
            Assert.That(AiRecoveryRules.CalculateReverseSteering(30f), Is.LessThan(0f));
            Assert.That(AiRecoveryRules.CalculateReverseSteering(-90f), Is.EqualTo(1f));
        }

        [Test]
        public void BrakingLimitedSpeed_RisesWithDistanceToASlowCorner()
        {
            var root = new GameObject("BrakingLine");
            try
            {
                // A long straight that ends in a hairpin: the corner is slow, the straight is fast.
                var racingLine = root.AddComponent<RacingLine>();
                var points = new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 80f), new Vector3(0f, 0f, 120f),
                    new Vector3(4f, 0f, 124f), new Vector3(8f, 0f, 120f), new Vector3(8f, 0f, 0f), new Vector3(4f, 0f, -4f)
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
                Assert.That(path.TryProjectLocal(new Vector3(0f, 0f, 10f), path.FindNearestSegment(new Vector3(0f, 0f, 10f)), 4, 4, out var far), Is.True);
                Assert.That(path.TryProjectLocal(new Vector3(0f, 0f, 105f), path.FindNearestSegment(new Vector3(0f, 0f, 105f)), 4, 4, out var near), Is.True);

                var farLimit = path.GetBrakingLimitedSpeed(far, 200f, 10f, 1f);
                var nearLimit = path.GetBrakingLimitedSpeed(near, 200f, 10f, 1f);
                var gentleBraker = path.GetBrakingLimitedSpeed(near, 200f, 5f, 1f);

                Assert.That(nearLimit, Is.LessThan(farLimit), "Closer to the hairpin the driver must already be slower.");
                Assert.That(gentleBraker, Is.LessThan(nearLimit), "A driver that brakes softer has to start braking earlier.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
