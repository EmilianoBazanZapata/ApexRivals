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
        public void OffTrackMovingAi_EntersRejoiningWithoutReset()
        {
            Assert.That(AiRecoveryRules.ShouldEnterRejoin(13f, 12f), Is.True);
            Assert.That(AiRecoveryRules.ShouldReset(0f, 2.5f, false, false), Is.False);
        }

        [Test]
        public void RejoiningAiMakingProgress_ResetsItsFailureTimerWithoutReset()
        {
            var timer = AiRecoveryRules.UpdateNoProgressTimer(true, true, 1f, 2f);

            Assert.That(timer, Is.Zero);
            Assert.That(AiRecoveryRules.ShouldReset(timer, 2.5f, true, false), Is.False);
        }

        [Test]
        public void OffTrackAiMovingTowardRacingLine_DoesNotAccumulateFailureTime()
        {
            var madeProgress = AiRecoveryRules.HasMeaningfulRecoveryProgress(
                18f, 17.5f,
                30f, 30f,
                80f, 80f,
                Vector3.zero, Vector3.zero,
                0.25f, 4f, 0.15f);
            var timer = AiRecoveryRules.UpdateNoProgressTimer(madeProgress, true, 3f, 2f);

            Assert.That(madeProgress, Is.True);
            Assert.That(timer, Is.Zero);
            Assert.That(AiRecoveryRules.ShouldReset(timer, 2.5f, true, false), Is.False);
        }

        [Test]
        public void OffTrackAiImprovingHeading_DoesNotAccumulateFailureTime()
        {
            var madeProgress = AiRecoveryRules.HasMeaningfulRecoveryProgress(
                18f, 18f,
                30f, 30f,
                70f, 64f,
                Vector3.zero, Vector3.zero,
                0.25f, 4f, 0.15f);

            Assert.That(madeProgress, Is.True);
            Assert.That(AiRecoveryRules.UpdateNoProgressTimer(madeProgress, true, 3f, 2f), Is.Zero);
        }

        [Test]
        public void SlowGrassMovement_IsMeaningfulRecoveryProgressWithoutHighSpeed()
        {
            var madeProgress = AiRecoveryRules.HasMeaningfulRecoveryProgress(
                18f, 18f,
                30f, 30f,
                70f, 70f,
                Vector3.zero, new Vector3(0.16f, 0f, 0f),
                0.25f, 4f, 0.15f);

            Assert.That(madeProgress, Is.True);
        }

        [Test]
        public void LongOffTrackRecovery_WithRepeatedProgressNeverPermitsReset()
        {
            var timer = 0f;
            for (var sample = 0; sample < 10; sample++)
            {
                var madeProgress = AiRecoveryRules.HasMeaningfulRecoveryProgress(
                    20f - sample, 19.5f - sample,
                    30f, 30f,
                    80f, 80f,
                    Vector3.zero, Vector3.zero,
                    0.25f, 4f, 0.15f);
                timer = AiRecoveryRules.UpdateNoProgressTimer(madeProgress, true, 1f, timer);
            }

            Assert.That(timer, Is.Zero);
            Assert.That(AiRecoveryRules.GetResetReason(timer, 2.5f, true, false), Is.EqualTo(AiResetReason.None));
        }

        [Test]
        public void RejoiningAiNearAndAlignedWithRoute_ReturnsToRacing()
        {
            Assert.That(AiRecoveryRules.CanResumeRacing(5f, 6f, 30f, 60f, true), Is.True);
        }

        [Test]
        public void StuckAi_EscalatesToReverseBeforeReset()
        {
            Assert.That(AiRecoveryRules.ShouldReverse(2.5f, 2.5f, false), Is.True);
            Assert.That(AiRecoveryRules.ShouldReset(2.5f, 2.5f, false, false), Is.False);
        }

        [Test]
        public void UnrecoverableAi_AfterReverseEventuallyRequestsReset()
        {
            Assert.That(AiRecoveryRules.ShouldReset(2.5f, 2.5f, true, false), Is.True);
            Assert.That(AiRecoveryRules.GetResetReason(2.5f, 2.5f, true, false), Is.EqualTo(AiResetReason.RecoveryFailed));
        }

        [Test]
        public void UpsideDownAi_AfterSustainedThresholdRequestsReset()
        {
            var inverted = AiRecoveryRules.IsUpsideDown(-0.5f, -0.35f);

            Assert.That(AiRecoveryRules.ShouldReset(0f, 2.5f, false, inverted), Is.True);
            Assert.That(AiRecoveryRules.GetResetReason(0f, 2.5f, false, inverted), Is.EqualTo(AiResetReason.UpsideDown));
        }

        [Test]
        public void RecoveryTarget_UsesForwardLineIndexInsteadOfWaypointZero()
        {
            var index = AiRecoveryRules.GetForwardRejoinTargetIndex(5, 4, 12);

            Assert.That(index, Is.EqualTo(9));
        }

        [Test]
        public void HairpinWrongBranch_GlobalNearestIndexIsRejectedByProgressSegment()
        {
            const int waypointCount = 149;
            const int segmentStart = 42;
            const int segmentEnd = 49;
            const int globalNearestWrongBranch = 104;
            const int validBranchCandidate = 46;

            Assert.That(AiRecoveryRules.IsWaypointIndexWithinProgressSegment(globalNearestWrongBranch, segmentStart, segmentEnd, 2, waypointCount), Is.False);
            Assert.That(AiRecoveryRules.IsWaypointIndexWithinProgressSegment(validBranchCandidate, segmentStart, segmentEnd, 2, waypointCount), Is.True);
        }

        [Test]
        public void SlightlyFartherValidCandidate_BeatsCloserInvalidBranchBecauseSegmentIsHardGate()
        {
            const int waypointCount = 149;
            var invalidBranchIsAllowed = AiRecoveryRules.IsWaypointIndexWithinProgressSegment(104, 42, 49, 2, waypointCount);
            var validCandidateScore = AiRecoveryRules.ScoreRecoveryCandidate(14f, 1f, false, 4);

            Assert.That(invalidBranchIsAllowed, Is.False);
            Assert.That(validCandidateScore, Is.GreaterThan(0f));
        }

        [Test]
        public void CandidateWithOppositeHeading_IsRejectedUnlessWrongWayRecoveryAllowsIt()
        {
            Assert.That(AiRecoveryRules.IsCandidateHeadingAcceptable(-0.9f, -0.2f), Is.False);
            Assert.That(AiRecoveryRules.IsCandidateHeadingAcceptable(0.5f, -0.2f), Is.True);
        }

        [Test]
        public void CandidateScoring_PrefersAShallowMergeOverAPerpendicularMerge()
        {
            var shallowScore = AiRecoveryRules.ScoreRecoveryCandidate(10f, 1f, 1f, false, 4);
            var perpendicularScore = AiRecoveryRules.ScoreRecoveryCandidate(10f, 1f, 0f, false, 4);

            Assert.That(shallowScore, Is.LessThan(perpendicularScore));
        }

        [Test]
        public void WrappedCheckpointSegment_ContainsFinishAndStartIndices()
        {
            Assert.That(AiRecoveryRules.IsWaypointIndexWithinProgressSegment(147, 140, 5, 2, 149), Is.True);
            Assert.That(AiRecoveryRules.IsWaypointIndexWithinProgressSegment(3, 140, 5, 2, 149), Is.True);
            Assert.That(AiRecoveryRules.IsWaypointIndexWithinProgressSegment(80, 140, 5, 2, 149), Is.False);
        }

        [Test]
        public void RejoinLookAhead_IsClampedToCheckpointSegmentForwardMargin()
        {
            var target = AiRecoveryRules.GetConstrainedForwardRejoinTargetIndex(48, 42, 49, 2, 4, 149);

            Assert.That(target, Is.EqualTo(51));
            Assert.That(AiRecoveryRules.IsWaypointIndexWithinProgressSegment(target, 42, 49, 2, 149), Is.True);
        }

        [Test]
        public void TrustedIndexRecovery_RejectsNearbyNonContiguousBranch()
        {
            const int trustedIndex = 20;
            const int wrongNearbyBranch = 70;
            var target = AiRecoveryRules.GetSequentialRecoveryTargetIndex(trustedIndex, 4, 10, 149);

            Assert.That(AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(wrongNearbyBranch, trustedIndex, 2, 10, 149), Is.False);
            Assert.That(target, Is.EqualTo(24));
            Assert.That(AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(target, trustedIndex, 2, 10, 149), Is.True);
        }

        [Test]
        public void RecoveryTarget_RemainsStableWhenTrustedIndexHasNotAdvanced()
        {
            var firstFrameTarget = AiRecoveryRules.GetSequentialRecoveryTargetIndex(72, 4, 10, 149);
            var secondFrameTarget = AiRecoveryRules.GetSequentialRecoveryTargetIndex(72, 4, 10, 149);

            Assert.That(secondFrameTarget, Is.EqualTo(firstFrameTarget));
        }

        [Test]
        public void SequentialRecoveryTarget_AdvancesOneWaypointAndNeverPastForwardWindow()
        {
            Assert.That(AiRecoveryRules.CanAdvanceSequentialRecoveryTarget(25, 20, 10, 149), Is.True);
            Assert.That(AiRecoveryRules.GetForwardIndexDistance(20, 25, 149), Is.EqualTo(5));
            Assert.That(AiRecoveryRules.CanAdvanceSequentialRecoveryTarget(31, 20, 10, 149), Is.False);
        }

        [Test]
        public void RecoveryWindow_BacktrackAllowanceIsSmallAndBounded()
        {
            Assert.That(AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(18, 20, 2, 10, 149), Is.True);
            Assert.That(AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(17, 20, 2, 10, 149), Is.False);
        }

        [Test]
        public void RecoveryWindow_WrapsAtStartFinishWithoutSelectingDistantBranch()
        {
            var target = AiRecoveryRules.GetSequentialRecoveryTargetIndex(147, 4, 10, 149);

            Assert.That(target, Is.EqualTo(2));
            Assert.That(AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(2, 147, 2, 10, 149), Is.True);
            Assert.That(AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(70, 147, 2, 10, 149), Is.False);
        }

        [Test]
        public void GlobalNearestDiagnostic_DoesNotReplaceTrustedRecoverySequence()
        {
            const int trustedIndex = 42;
            const int globalNearestIndex = 104;
            var target = AiRecoveryRules.GetSequentialRecoveryTargetIndex(trustedIndex, 4, 10, 149);

            Assert.That(globalNearestIndex, Is.Not.EqualTo(target));
            Assert.That(AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(globalNearestIndex, trustedIndex, 2, 10, 149), Is.False);
        }

        [Test]
        public void ResetSafeIndex_ReinitializesLocalSequentialRecoveryWithoutWaypointZero()
        {
            var target = AiRecoveryRules.GetSequentialRecoveryTargetIndex(118, 4, 10, 149);

            Assert.That(target, Is.EqualTo(122));
            Assert.That(target, Is.Not.EqualTo(0));
        }

        [Test]
        public void RecoveryTargetDistanceSanity_RejectsADramaticallyLongShortcut()
        {
            Assert.That(AiRecoveryRules.IsRecoveryTargetDistanceSane(20f, 4f, 4, 3f), Is.True);
            Assert.That(AiRecoveryRules.IsRecoveryTargetDistanceSane(100f, 4f, 4, 3f), Is.False);
        }

        [Test]
        public void AdaptiveArcLookahead_StraightAtSpeedAllowsLongerTargetThanTightCorner()
        {
            var straightLookahead = AiRecoveryRules.CalculateAdaptiveArcLookahead(20f, 20f, 6f, 16f, 0f);
            var tightCornerLookahead = AiRecoveryRules.CalculateAdaptiveArcLookahead(20f, 20f, 6f, 16f, 1f);

            Assert.That(straightLookahead, Is.EqualTo(16f));
            Assert.That(tightCornerLookahead, Is.EqualTo(6f));
        }

        [Test]
        public void AdaptiveArcLookahead_RejoiningRangeIsShorterThanRacingRange()
        {
            var racingLookahead = AiRecoveryRules.CalculateAdaptiveArcLookahead(12f, 20f, 6f, 16f, 0f);
            var rejoiningLookahead = AiRecoveryRules.CalculateAdaptiveArcLookahead(12f, 12f, 4f, 8f, 0f);

            Assert.That(rejoiningLookahead, Is.LessThan(racingLookahead));
        }

        [Test]
        public void ArcChordGuard_ShortensTargetAcrossTightBend()
        {
            Assert.That(AiRecoveryRules.ShouldShortenArcLookahead(0.4f, 0.55f), Is.True);
            Assert.That(AiRecoveryRules.ShouldShortenArcLookahead(0.8f, 0.55f), Is.False);
        }

        [Test]
        public void ResetRequest_DuringPostResetGraceOrCooldown_IsRejected()
        {
            Assert.That(AiRecoveryRules.CanRequestPhysicalReset(true, 2f, 0f), Is.False);
            Assert.That(AiRecoveryRules.CanRequestPhysicalReset(true, 0f, 5f), Is.False);
            Assert.That(AiRecoveryRules.CanRequestPhysicalReset(true, 0f, 0f), Is.True);
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
        public void IdealDriver_SequentialProgressAdvancesAndWrapsWithoutCircuitJump()
        {
            Assert.That(IdealRacingDriverRules.IsSequentialAdvance(431, 434, 8, 900), Is.True);
            Assert.That(IdealRacingDriverRules.IsSequentialAdvance(898, 2, 8, 900), Is.True);
            Assert.That(IdealRacingDriverRules.IsSequentialAdvance(431, 700, 8, 900), Is.False);
        }

        [Test]
        public void IdealDriver_PersistentTargetRemainsUnchangedBeforeReachOrPass()
        {
            var shouldAdvance = IdealRacingDriverRules.ShouldAdvancePersistentTarget(
                hasValidTarget: true,
                reachedTarget: false,
                passedTarget: false,
                targetBehindProgress: false);

            Assert.That(shouldAdvance, Is.False);
        }

        [Test]
        public void IdealDriver_CloseSamplesAreSkippedUntilMinimumArcLookaheadIsMet()
        {
            var root = CreateIdealLine(out var line,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f),
                new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, 12f),
                new Vector3(12f, 0f, 12f), new Vector3(12f, 0f, 0f));
            try
            {
                Assert.That(IdealRacingPath.TryBuild(line, 4f, 20f, 6f, 18f, out var path), Is.True);
                Assert.That(path.TryProjectLocal(Vector3.zero, 0, 0, 1, out var projection), Is.True);

                var targetIndex = path.GetForwardSampleAtArcDistance(projection, 10f, out var arcDistance);

                Assert.That(targetIndex, Is.EqualTo(3));
                Assert.That(arcDistance, Is.EqualTo(12f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdealDriver_ReachedTargetAdvancesImmediately()
        {
            Assert.That(IdealRacingDriverRules.HasReachedPersistentTarget(2.9f, 3f), Is.True);
            Assert.That(IdealRacingDriverRules.ShouldAdvancePersistentTarget(true, true, false, false), Is.True);
        }

        [Test]
        public void IdealDriver_PassedTargetAdvancesOnlyWithSequentialPathProgress()
        {
            var passed = IdealRacingDriverRules.HasPassedPersistentTarget(
                new Vector3(0f, 0f, 14f),
                new Vector3(0f, 0f, 12f),
                Vector3.forward,
                currentIndex: 4,
                targetIndex: 3,
                maximumSequentialAdvance: 4,
                count: 12);
            var nonSequential = IdealRacingDriverRules.HasPassedPersistentTarget(
                new Vector3(0f, 0f, 14f),
                new Vector3(0f, 0f, 12f),
                Vector3.forward,
                currentIndex: 9,
                targetIndex: 3,
                maximumSequentialAdvance: 4,
                count: 12);

            Assert.That(passed, Is.True);
            Assert.That(nonSequential, Is.False);
        }

        [Test]
        public void IdealDriver_TargetBehindCurrentProgressAdvances()
        {
            var behind = IdealRacingDriverRules.IsTargetAtOrBehindProgress(
                currentIndex: 5,
                targetIndex: 3,
                maximumSequentialAdvance: 4,
                count: 12);

            Assert.That(behind, Is.True);
            Assert.That(IdealRacingDriverRules.ShouldAdvancePersistentTarget(true, false, false, behind), Is.True);
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
        public void IdealDriver_WaypointsArePassThroughReferencesRatherThanBrakeDestinations()
        {
            var reachedWaypoint = IdealRacingDriverRules.HasReachedPersistentTarget(1f, 3f);
            var speedCommand = IdealRacingDriverRules.CalculateSpeedCommand(8f, 20f, 1.4f, 1.2f);

            Assert.That(reachedWaypoint, Is.True);
            Assert.That(speedCommand.Throttle, Is.GreaterThan(0f));
            Assert.That(speedCommand.Brake, Is.Zero);
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
        public void IdealDriver_ResetTargetStartsMeaningfullyAheadOfSafeProjection()
        {
            var root = CreateIdealLine(out var line,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f),
                new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, 12f),
                new Vector3(12f, 0f, 12f), new Vector3(12f, 0f, 0f));
            try
            {
                Assert.That(IdealRacingPath.TryBuild(line, 4f, 20f, 6f, 18f, out var path), Is.True);
                Assert.That(path.TryProjectLocal(Vector3.zero, 0, 0, 1, out var safeProjection), Is.True);
                var targetIndex = path.GetForwardSampleAtArcDistance(safeProjection, 10f, out var arcDistance);

                Assert.That(targetIndex, Is.Not.EqualTo(safeProjection.SegmentIndex));
                Assert.That(arcDistance, Is.GreaterThanOrEqualTo(10f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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

        [Test]
        public void IdealDriver_UpcomingCurvaturePreviewSeesLowerSpeedBeforeTurn()
        {
            var root = CreateIdealLine(out var line,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f),
                new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, 0f));
            try
            {
                Assert.That(IdealRacingPath.TryBuild(line, 4f, 20f, 6f, 18f, out var path), Is.True);
                Assert.That(path.TryProjectLocal(new Vector3(0f, 0f, 4f), 1, 1, 1, out var projection), Is.True);

                Assert.That(path.GetMinimumTargetSpeedAhead(projection, 30f), Is.LessThan(path.GetSample(projection.SegmentIndex).TargetSpeed));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdealDriver_SpeedFactorCapsSpeedAndResetKeepsSafeProgress()
        {
            var full = IdealRacingDriverRules.CalculateProfileSpeed(20f, 6f, 0f, 18f, 1f);
            var conservative = IdealRacingDriverRules.CalculateProfileSpeed(20f, 6f, 0f, 18f, 0.5f);
            var resetAnchor = IdealRacingDriverRules.ResolveResetAnchorIndex(118, 0, 900);

            Assert.That(conservative, Is.EqualTo(full * 0.5f));
            Assert.That(resetAnchor, Is.EqualTo(118));
            Assert.That(resetAnchor, Is.Not.EqualTo(0));
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
