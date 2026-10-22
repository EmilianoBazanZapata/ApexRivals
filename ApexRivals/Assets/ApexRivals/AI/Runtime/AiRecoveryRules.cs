using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    public static class AiRecoveryRules
    {

        /// <summary>
        /// Escalation used by the driver: a flipped car resets at once; a stuck car first reverses and
        /// resets only if it is still stuck after reversing; a car that stays off track too long resets.
        /// </summary>
        public static AiRecoveryAction ResolveRecoveryAction(
            bool isUpsideDown,
            float stuckTimer,
            float stuckDuration,
            bool reversedRecently,
            float offTrackTimer,
            float offTrackResetDuration,
            out AiResetReason reason)
        {
            if (isUpsideDown)
            {
                reason = AiResetReason.UpsideDown;
                return AiRecoveryAction.Reset;
            }

            if (stuckTimer >= Mathf.Max(0f, stuckDuration))
            {
                reason = reversedRecently ? AiResetReason.RecoveryFailed : AiResetReason.StuckNoProgress;
                return reversedRecently ? AiRecoveryAction.Reset : AiRecoveryAction.Reverse;
            }

            if (offTrackResetDuration > 0f && offTrackTimer >= offTrackResetDuration)
            {
                reason = AiResetReason.OffTrack;
                return AiRecoveryAction.Reset;
            }

            reason = AiResetReason.None;
            return AiRecoveryAction.None;
        }

        /// <summary>
        /// Metres ahead of the trouble spot where a reset places the car. A stuck car is moved past the
        /// obstacle that trapped it, and each repeated reset at the same spot moves it further.
        /// </summary>
        public static float CalculateResetAdvance(AiResetReason reason, int repeatedResets)
        {
            var repeats = Mathf.Max(0, repeatedResets);
            var baseAdvance = reason == AiResetReason.StuckNoProgress || reason == AiResetReason.RecoveryFailed ? 10f : 4f;
            return Mathf.Min(60f, baseAdvance + 15f * repeats);
        }

        /// <summary>
        /// Keeps a reset from jumping over the next checkpoint the car still has to cross; otherwise the
        /// lap would never count. <paramref name="gapToNextCheckpoint"/> is the distance along the lap
        /// (negative or unknown means no limit).
        /// </summary>
        public static float ClampResetAdvanceToCheckpoint(float advance, float gapToNextCheckpoint, float margin)
        {
            if (gapToNextCheckpoint <= 0f || float.IsNaN(gapToNextCheckpoint))
            {
                return advance;
            }

            return Mathf.Min(advance, Mathf.Max(0f, gapToNextCheckpoint - Mathf.Max(0f, margin)));
        }

        /// <summary>
        /// Sideways offset for successive reset attempts: centre first, then alternating right/left in
        /// growing steps, never beyond the usable track width.
        /// </summary>
        public static float CalculateResetLateralOffset(int attempt, float maximumOffset)
        {
            if (attempt <= 0)
            {
                return 0f;
            }

            var step = 3f * ((attempt + 1) / 2);
            var side = attempt % 2 == 1 ? 1f : -1f;
            return side * Mathf.Min(step, Mathf.Max(0f, maximumOffset));
        }

        /// <summary>Accumulates time spent beyond the off-track distance; returning to the track clears it.</summary>
        public static float UpdateOffTrackTimer(float distanceFromLine, float offTrackDistance, float deltaTime, float currentTimer)
        {
            return distanceFromLine > Mathf.Max(0f, offTrackDistance) ? currentTimer + Mathf.Max(0f, deltaTime) : 0f;
        }

        /// <summary>
        /// Steering while reversing: the front wheels turn opposite to the heading error so the nose
        /// swings back toward the ideal line.
        /// </summary>
        public static float CalculateReverseSteering(float signedHeadingErrorDegrees)
        {
            return Mathf.Clamp(-signedHeadingErrorDegrees / 45f, -1f, 1f);
        }

        public static bool IsUpsideDown(float upAlignment, float invertedThreshold)
        {
            return upAlignment <= Mathf.Clamp(invertedThreshold, -1f, 1f);
        }
    }
}
