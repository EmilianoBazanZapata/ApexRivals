using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>One other car expressed in the driver's path frame.</summary>
    public readonly struct AiTrafficCar
    {
        public AiTrafficCar(float gap, float lateral, float speed)
        {
            Gap = gap;
            Lateral = lateral;
            Speed = speed;
        }

        /// <summary>Metres along the lap from the driver to this car; positive means ahead.</summary>
        public float Gap { get; }

        /// <summary>Signed metres from the ideal line; positive is to the right.</summary>
        public float Lateral { get; }

        public float Speed { get; }
    }

    public readonly struct AiTrafficAssessment
    {
        public AiTrafficAssessment(
            bool hasCarAhead,
            float aheadGap,
            float aheadSpeed,
            float aheadLateral,
            bool leftBlocked,
            bool rightBlocked,
            bool hasAttacker,
            float attackerLateral)
        {
            HasCarAhead = hasCarAhead;
            AheadGap = aheadGap;
            AheadSpeed = aheadSpeed;
            AheadLateral = aheadLateral;
            LeftBlocked = leftBlocked;
            RightBlocked = rightBlocked;
            HasAttacker = hasAttacker;
            AttackerLateral = attackerLateral;
        }

        public bool HasCarAhead { get; }
        public float AheadGap { get; }
        public float AheadSpeed { get; }
        public float AheadLateral { get; }
        public bool LeftBlocked { get; }
        public bool RightBlocked { get; }
        public bool HasAttacker { get; }
        public float AttackerLateral { get; }
    }

    /// <summary>
    /// Pure wheel-to-wheel decisions: who is in the way, how close to follow, which side to pass on
    /// and when to defend. Distances are in the shared ideal-path frame so curves do not distort them.
    /// </summary>
    public static class AiRacecraftRules
    {
        /// <summary>Two cars overlap laterally when their centres are closer than this.</summary>
        public const float LaneOverlapWidth = 2.8f;

        public const float LookAheadDistance = 45f;
        public const float SideAwarenessDistance = 7f;
        public const float SideAwarenessWidth = 4.5f;
        public const float AttackerAwarenessDistance = 12f;
        public const float MinimumFollowGap = 6f;

        /// <summary>Below this speed a car ahead is treated as an obstacle to drive around.</summary>
        public const float SlowCarSpeed = 4f;

        /// <summary>Speed kept while moving across to pass a car that still overlaps the driver's lane.</summary>
        public const float PassingSpeed = 8f;

        public static AiTrafficAssessment Assess(float selfLateral, float selfSpeed, IReadOnlyList<AiTrafficCar> cars)
        {
            var hasAhead = false;
            var aheadGap = float.PositiveInfinity;
            var aheadSpeed = 0f;
            var aheadLateral = 0f;
            var leftBlocked = false;
            var rightBlocked = false;
            var hasAttacker = false;
            var attackerLateral = 0f;
            var attackerGap = float.NegativeInfinity;

            if (cars == null)
            {
                return default;
            }

            for (var index = 0; index < cars.Count; index++)
            {
                var car = cars[index];
                var lateralDelta = car.Lateral - selfLateral;

                if (car.Gap > 0f && car.Gap < LookAheadDistance && Mathf.Abs(lateralDelta) < LaneOverlapWidth && car.Gap < aheadGap)
                {
                    hasAhead = true;
                    aheadGap = car.Gap;
                    aheadSpeed = car.Speed;
                    aheadLateral = car.Lateral;
                }

                if (Mathf.Abs(car.Gap) < SideAwarenessDistance && Mathf.Abs(lateralDelta) < SideAwarenessWidth && Mathf.Abs(lateralDelta) >= LaneOverlapWidth * 0.5f)
                {
                    if (lateralDelta < 0f)
                    {
                        leftBlocked = true;
                    }
                    else
                    {
                        rightBlocked = true;
                    }
                }

                if (car.Gap < 0f && car.Gap > -AttackerAwarenessDistance && car.Speed > selfSpeed + 0.5f && car.Gap > attackerGap)
                {
                    hasAttacker = true;
                    attackerGap = car.Gap;
                    attackerLateral = car.Lateral;
                }
            }

            return new AiTrafficAssessment(hasAhead, hasAhead ? aheadGap : 0f, aheadSpeed, aheadLateral, leftBlocked, rightBlocked, hasAttacker, attackerLateral);
        }

        /// <summary>
        /// Speed that settles the driver at a time gap behind a slower car. Closing too fast lowers it
        /// below the leader's speed; a large gap lets the driver keep closing.
        /// </summary>
        public static float CalculateFollowSpeed(float aheadGap, float aheadSpeed, float selfSpeed, float followTimeGap)
        {
            var desiredGap = MinimumFollowGap + Mathf.Max(0f, selfSpeed) * Mathf.Max(0f, followTimeGap);
            return Mathf.Max(0f, aheadSpeed + (aheadGap - desiredGap) * 0.8f);
        }

        /// <summary>
        /// A driver attempts a pass when it wants to go meaningfully faster than the car ahead.
        /// Aggressive drivers accept a smaller speed advantage.
        /// </summary>
        public static bool ShouldAttemptOvertake(float desiredSpeed, float aheadSpeed, float aheadGap, float aggression)
        {
            var requiredAdvantage = Mathf.Lerp(4f, 0.75f, Mathf.Clamp01(aggression));
            return aheadGap < LookAheadDistance * 0.6f && desiredSpeed - aheadSpeed >= requiredAdvantage;
        }

        /// <summary>
        /// Passing is attempted where the track is close to full speed; in corners side-by-side cars
        /// collide, so drivers follow until the next straight.
        /// </summary>
        public static bool IsPassingZone(float freeSpeed, float straightSpeed)
        {
            return straightSpeed > 0f && freeSpeed >= straightSpeed * 0.8f;
        }

        /// <summary>
        /// Picks the free side that deviates least from the ideal line. Returns false when both sides
        /// are blocked or would leave the usable track width.
        /// </summary>
        public static bool TryChooseOvertakeOffset(
            float aheadLateral,
            bool leftBlocked,
            bool rightBlocked,
            float overtakeOffset,
            float maximumOffset,
            out float offset)
        {
            return TryChooseOvertakeOffset(aheadLateral, leftBlocked, rightBlocked, overtakeOffset, maximumOffset, 0, out offset);
        }

        /// <summary>
        /// Same choice, but a pass already committed to one side (preferredSide +1 right, -1 left) keeps that
        /// side while it is still usable instead of switching whenever the other side looks marginally shorter.
        /// </summary>
        public static bool TryChooseOvertakeOffset(
            float aheadLateral,
            bool leftBlocked,
            bool rightBlocked,
            float overtakeOffset,
            float maximumOffset,
            int preferredSide,
            out float offset)
        {
            var limit = Mathf.Max(0f, maximumOffset);
            var right = aheadLateral + Mathf.Max(LaneOverlapWidth, overtakeOffset);
            var left = aheadLateral - Mathf.Max(LaneOverlapWidth, overtakeOffset);
            var rightUsable = !rightBlocked && right <= limit;
            var leftUsable = !leftBlocked && left >= -limit;

            if (preferredSide > 0 && rightUsable)
            {
                offset = right;
                return true;
            }

            if (preferredSide < 0 && leftUsable)
            {
                offset = left;
                return true;
            }

            if (rightUsable && (!leftUsable || Mathf.Abs(right) <= Mathf.Abs(left)))
            {
                offset = right;
                return true;
            }

            if (leftUsable)
            {
                offset = left;
                return true;
            }

            offset = 0f;
            return false;
        }

        /// <summary>
        /// Aggressive drivers drift toward an attacker's line to cover it, never further than the track allows.
        /// </summary>
        public static float CalculateDefendOffset(float selfLateral, float attackerLateral, float aggression, float maximumOffset)
        {
            if (aggression < 0.6f)
            {
                return selfLateral;
            }

            var cover = Mathf.Clamp(attackerLateral - selfLateral, -1.5f, 1.5f);
            return Mathf.Clamp(selfLateral + cover, -maximumOffset, maximumOffset);
        }

        /// <summary>
        /// True when no car sits on the ideal line close enough that returning to it would cut across
        /// that car. Used to finish an overtake only once the pass is complete.
        /// </summary>
        public static bool IsIdealLineClear(IReadOnlyList<AiTrafficCar> cars, float behindDistance, float aheadDistance)
        {
            if (cars == null)
            {
                return true;
            }

            for (var index = 0; index < cars.Count; index++)
            {
                var car = cars[index];
                if (car.Gap > -Mathf.Abs(behindDistance) && car.Gap < Mathf.Abs(aheadDistance) && Mathf.Abs(car.Lateral) < LaneOverlapWidth)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Seconds until contact with a car ahead at the current closing speed; infinity if not closing.</summary>
        public static float CalculateTimeToContact(float gap, float closingSpeed)
        {
            return closingSpeed > 0.01f && gap > 0f ? gap / closingSpeed : float.PositiveInfinity;
        }

        /// <summary>
        /// Highest speed from which the car can still stop before an obstacle, given its braking deceleration.
        /// </summary>
        public static float CalculateObstacleSpeedLimit(float obstacleDistance, float stoppingMargin, float deceleration)
        {
            var usable = Mathf.Max(0f, obstacleDistance - Mathf.Max(0f, stoppingMargin));
            return Mathf.Sqrt(2f * Mathf.Max(0.1f, deceleration) * usable);
        }

        /// <summary>Signed gap along a closed lap, wrapped to the shorter direction.</summary>
        public static float WrapLapGap(float otherDistance, float selfDistance, float lapLength)
        {
            if (lapLength <= 0f)
            {
                return otherDistance - selfDistance;
            }

            var gap = Mathf.Repeat(otherDistance - selfDistance, lapLength);
            return gap > lapLength * 0.5f ? gap - lapLength : gap;
        }
    }
}
