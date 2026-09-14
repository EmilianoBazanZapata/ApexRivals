using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    public static class AiRecoveryRules
    {
        private const float CandidateHeadingPenalty = 12f;
        private const float MergeAnglePenalty = 8f;
        private const float BehindCandidatePenalty = 10f;
        private const float ForwardProgressPenalty = 0.1f;

        public static bool ShouldEnterRejoin(float distanceFromRacingLine, float enterDistance)
        {
            return distanceFromRacingLine > Mathf.Max(0f, enterDistance);
        }

        public static int GetForwardRejoinTargetIndex(int nearestLineIndex, int lookAheadWaypointCount, int waypointCount)
        {
            return AiDriverRules.WrapWaypointIndex(nearestLineIndex + Mathf.Max(1, lookAheadWaypointCount), waypointCount);
        }

        public static int GetForwardIndexDistance(int fromIndex, int toIndex, int waypointCount)
        {
            if (waypointCount <= 0)
            {
                return 0;
            }

            return AiDriverRules.WrapWaypointIndex(toIndex - fromIndex, waypointCount);
        }

        public static bool IsWaypointIndexWithinProgressSegment(int waypointIndex, int segmentStartIndex, int segmentEndIndex, int forwardMargin, int waypointCount)
        {
            if (waypointCount <= 0)
            {
                return false;
            }

            var segmentLength = GetForwardIndexDistance(segmentStartIndex, segmentEndIndex, waypointCount);
            var candidateDistance = GetForwardIndexDistance(segmentStartIndex, waypointIndex, waypointCount);
            return candidateDistance <= segmentLength + Mathf.Max(0, forwardMargin);
        }

        public static bool IsWaypointIndexWithinContinuityWindow(int waypointIndex, int currentWaypointIndex, int backwardWindow, int forwardWindow, int waypointCount)
        {
            if (waypointCount <= 0)
            {
                return false;
            }

            var forwardDistance = GetForwardIndexDistance(currentWaypointIndex, waypointIndex, waypointCount);
            var backwardDistance = GetForwardIndexDistance(waypointIndex, currentWaypointIndex, waypointCount);
            return forwardDistance <= Mathf.Max(0, forwardWindow) || backwardDistance <= Mathf.Max(0, backwardWindow);
        }

        public static bool IsWaypointIndexInsideRecoveryWindow(int waypointIndex, int trustedWaypointIndex, int backwardAllowance, int forwardAllowance, int waypointCount)
        {
            return IsWaypointIndexWithinContinuityWindow(waypointIndex, trustedWaypointIndex, Mathf.Max(0, backwardAllowance), Mathf.Max(0, forwardAllowance), waypointCount);
        }

        public static int GetSequentialRecoveryTargetIndex(int trustedWaypointIndex, int lookAheadWaypointCount, int forwardAllowance, int waypointCount)
        {
            if (waypointCount <= 0)
            {
                return 0;
            }

            var forwardDistance = Mathf.Min(Mathf.Max(1, lookAheadWaypointCount), Mathf.Max(1, forwardAllowance));
            return AiDriverRules.WrapWaypointIndex(trustedWaypointIndex + forwardDistance, waypointCount);
        }

        public static bool CanAdvanceSequentialRecoveryTarget(int nextTargetIndex, int trustedWaypointIndex, int forwardAllowance, int waypointCount)
        {
            return GetForwardIndexDistance(trustedWaypointIndex, nextTargetIndex, waypointCount) <= Mathf.Max(1, forwardAllowance);
        }

        public static bool IsRecoveryTargetDistanceSane(float distanceToTarget, float averageWaypointSpacing, int forwardIndexDelta, float spacingMultiplier)
        {
            var expectedDistance = Mathf.Max(averageWaypointSpacing, 0.001f) * Mathf.Max(1, forwardIndexDelta) * Mathf.Max(1f, spacingMultiplier);
            return distanceToTarget <= expectedDistance;
        }

        public static float CalculateAdaptiveArcLookahead(float speed, float speedForMaximumLookahead, float minimumLookahead, float maximumLookahead, float upcomingCurvature)
        {
            var minimum = Mathf.Max(0f, minimumLookahead);
            var maximum = Mathf.Max(minimum, maximumLookahead);
            var speedFactor = Mathf.Clamp01(speed / Mathf.Max(speedForMaximumLookahead, 0.001f));
            var speedLookahead = Mathf.Lerp(minimum, maximum, speedFactor);
            return Mathf.Lerp(speedLookahead, minimum, Mathf.Clamp01(upcomingCurvature));
        }

        public static bool ShouldShortenArcLookahead(float chordRatio, float minimumChordRatio)
        {
            return chordRatio < Mathf.Clamp01(minimumChordRatio);
        }

        public static int GetConstrainedForwardRejoinTargetIndex(int candidateIndex, int segmentStartIndex, int segmentEndIndex, int forwardMargin, int lookAheadWaypointCount, int waypointCount)
        {
            if (waypointCount <= 0)
            {
                return 0;
            }

            var maximumDistance = GetForwardIndexDistance(segmentStartIndex, segmentEndIndex, waypointCount) + Mathf.Max(0, forwardMargin);
            var candidateDistance = Mathf.Min(GetForwardIndexDistance(segmentStartIndex, candidateIndex, waypointCount), maximumDistance);
            var targetDistance = Mathf.Min(candidateDistance + Mathf.Max(1, lookAheadWaypointCount), maximumDistance);
            return AiDriverRules.WrapWaypointIndex(segmentStartIndex + targetDistance, waypointCount);
        }

        public static bool IsCandidateHeadingAcceptable(float headingAlignment, float minimumHeadingAlignment)
        {
            return headingAlignment >= Mathf.Clamp(minimumHeadingAlignment, -1f, 1f);
        }

        public static float ScoreRecoveryCandidate(float distance, float headingAlignment, bool isBehindCurrentProgress, int forwardProgressDistance)
        {
            return ScoreRecoveryCandidate(distance, headingAlignment, 1f, isBehindCurrentProgress, forwardProgressDistance);
        }

        public static float ScoreRecoveryCandidate(float distance, float headingAlignment, float mergeAlignment, bool isBehindCurrentProgress, int forwardProgressDistance)
        {
            var headingPenalty = (1f - Mathf.Clamp(headingAlignment, -1f, 1f)) * CandidateHeadingPenalty;
            var mergePenalty = (1f - Mathf.Clamp(mergeAlignment, -1f, 1f)) * MergeAnglePenalty;
            var behindPenalty = isBehindCurrentProgress ? BehindCandidatePenalty : 0f;
            return Mathf.Max(0f, distance) + headingPenalty + mergePenalty + behindPenalty + Mathf.Max(0, forwardProgressDistance) * ForwardProgressPenalty;
        }

        public static bool CanResumeRacing(float distanceFromRacingLine, float exitDistance, float headingError, float maximumHeadingError, bool hasMadeProgress)
        {
            return hasMadeProgress && distanceFromRacingLine <= Mathf.Max(0f, exitDistance) && headingError <= Mathf.Clamp(maximumHeadingError, 0f, 180f);
        }

        public static float UpdateNoProgressTimer(bool recoveryIsMakingProgress, bool expectedToMove, float deltaTime, float currentTimer)
        {
            if (recoveryIsMakingProgress || !expectedToMove)
            {
                return 0f;
            }

            return currentTimer + Mathf.Max(0f, deltaTime);
        }

        public static bool HasMadeProgress(float previousTargetDistance, float currentTargetDistance, float minimumDistanceImprovement)
        {
            return currentTargetDistance <= previousTargetDistance - Mathf.Max(0f, minimumDistanceImprovement);
        }

        public static bool HasMeaningfulRecoveryProgress(float referenceDistanceToLine, float currentDistanceToLine, float referenceDistanceToTarget, float currentDistanceToTarget, float referenceHeadingError, float currentHeadingError, Vector3 referencePosition, Vector3 currentPosition, float distanceImprovementThreshold, float headingImprovementThreshold, float movementThreshold)
        {
            var distanceThreshold = Mathf.Max(0f, distanceImprovementThreshold);
            var lineImproved = HasMadeProgress(referenceDistanceToLine, currentDistanceToLine, distanceThreshold);
            var targetImproved = HasMadeProgress(referenceDistanceToTarget, currentDistanceToTarget, distanceThreshold);
            var headingImproved = currentHeadingError <= referenceHeadingError - Mathf.Max(0f, headingImprovementThreshold);
            var hasMoved = (currentPosition - referencePosition).sqrMagnitude >= Mathf.Max(0f, movementThreshold) * Mathf.Max(0f, movementThreshold);
            return lineImproved || targetImproved || headingImproved || hasMoved;
        }

        public static bool ShouldReverse(float noProgressTimer, float timeout, bool hasAlreadyReversed)
        {
            return !hasAlreadyReversed && noProgressTimer >= Mathf.Max(0f, timeout);
        }

        public static bool ShouldReset(float noProgressTimer, float timeout, bool hasAlreadyReversed, bool isUpsideDown)
        {
            return isUpsideDown || (hasAlreadyReversed && noProgressTimer >= Mathf.Max(0f, timeout));
        }

        public static AiResetReason GetResetReason(float noProgressTimer, float timeout, bool hasAlreadyReversed, bool isUpsideDown)
        {
            if (isUpsideDown)
            {
                return AiResetReason.UpsideDown;
            }

            return hasAlreadyReversed && noProgressTimer >= Mathf.Max(0f, timeout) ? AiResetReason.RecoveryFailed : AiResetReason.None;
        }

        public static bool CanRequestPhysicalReset(bool resetConditionMet, float postResetGraceRemaining, float resetCooldownRemaining)
        {
            return resetConditionMet && postResetGraceRemaining <= 0f && resetCooldownRemaining <= 0f;
        }

        public static bool IsUpsideDown(float upAlignment, float invertedThreshold)
        {
            return upAlignment <= Mathf.Clamp(invertedThreshold, -1f, 1f);
        }
    }
}
