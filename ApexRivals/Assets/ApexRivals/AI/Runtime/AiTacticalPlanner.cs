using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>What the planner needs to know about the driver and its temperament.</summary>
    public readonly struct AiTacticalInput
    {
        public AiTacticalInput(
            float crossTrackError,
            float lateralOffset,
            float speed,
            float freeSpeed,
            float upcomingAbsoluteHeadingChange,
            float aggression,
            float overtakeOffset,
            float maximumOffset,
            float followTimeGap)
        {
            CrossTrackError = crossTrackError;
            LateralOffset = lateralOffset;
            Speed = speed;
            FreeSpeed = freeSpeed;
            UpcomingAbsoluteHeadingChange = upcomingAbsoluteHeadingChange;
            Aggression = aggression;
            OvertakeOffset = overtakeOffset;
            MaximumOffset = maximumOffset;
            FollowTimeGap = followTimeGap;
        }

        /// <summary>Where the car actually is, sideways from the ideal line.</summary>
        public float CrossTrackError { get; }

        /// <summary>Where the car is currently aiming, sideways from the ideal line.</summary>
        public float LateralOffset { get; }

        public float Speed { get; }

        /// <summary>Speed the track allows here without traffic.</summary>
        public float FreeSpeed { get; }

        /// <summary>Total bending in the lane-change preview, in degrees, without cancelling opposite turns.</summary>
        public float UpcomingAbsoluteHeadingChange { get; }

        public float Aggression { get; }
        public float OvertakeOffset { get; }
        public float MaximumOffset { get; }
        public float FollowTimeGap { get; }
    }

    public readonly struct AiTacticalDecision
    {
        public AiTacticalDecision(AiDrivingState state, float lateralOffset, float trafficSpeedLimit)
        {
            State = state;
            LateralOffset = lateralOffset;
            TrafficSpeedLimit = trafficSpeedLimit;
        }

        /// <summary>Racing, Following, Overtaking, Defending or Avoiding.</summary>
        public AiDrivingState State { get; }

        /// <summary>Line to aim for, sideways from the ideal line.</summary>
        public float LateralOffset { get; }

        /// <summary>Speed cap imposed by traffic; infinity when the way is clear.</summary>
        public float TrafficSpeedLimit { get; }
    }

    /// <summary>
    /// Wheel-to-wheel decisions: follow a slower car, pass it on a straight, go around a stopped one,
    /// cover an attacker or avoid an imminent collision. Remembers an overtake in progress so the driver
    /// does not cut back across a car it is still alongside. Pure logic, no Unity objects.
    /// </summary>
    public sealed class AiTacticalPlanner
    {
        private const float AvoidTimeToContact = 0.9f;
        private const float AvoidGap = 14f;
        private const float BlockedSpeedFactor = 0.85f;
        private const float DefendAggression = 0.6f;
        private const float LineClearBehind = 6f;
        private const float LineClearAhead = 10f;

        private bool _overtaking;
        private float _offset;

        public bool IsOvertaking => _overtaking;

        public void Reset()
        {
            _overtaking = false;
            _offset = 0f;
        }

        public AiTacticalDecision Decide(in AiTacticalInput input, IReadOnlyList<AiTrafficCar> cars)
        {
            var traffic = AiRacecraftRules.Assess(input.CrossTrackError, input.Speed, cars);
            if (traffic.HasCarAhead)
            {
                return DecideBehindCar(input, traffic);
            }

            if (_overtaking)
            {
                // Hold the passing line until returning to the ideal line no longer cuts across anyone.
                if (!AiRacecraftRules.IsIdealLineClear(cars, LineClearBehind, LineClearAhead))
                {
                    return new AiTacticalDecision(AiDrivingState.Overtaking, _offset, float.PositiveInfinity);
                }

                Reset();
                return new AiTacticalDecision(AiDrivingState.Racing, 0f, float.PositiveInfinity);
            }

            if (traffic.HasAttacker && input.Aggression >= DefendAggression)
            {
                _offset = AiRacecraftRules.CalculateDefendOffset(input.LateralOffset, traffic.AttackerLateral, input.Aggression, input.MaximumOffset);
                return new AiTacticalDecision(AiDrivingState.Defending, _offset, float.PositiveInfinity);
            }

            _offset = 0f;
            return new AiTacticalDecision(AiDrivingState.Racing, 0f, float.PositiveInfinity);
        }

        private AiTacticalDecision DecideBehindCar(in AiTacticalInput input, AiTrafficAssessment traffic)
        {
            var closingSpeed = input.Speed - traffic.AheadSpeed;
            var timeToContact = AiRacecraftRules.CalculateTimeToContact(traffic.AheadGap, closingSpeed);
            var canPass = AiRacecraftRules.TryChooseOvertakeOffset(
                traffic.AheadLateral,
                traffic.LeftBlocked,
                traffic.RightBlocked,
                input.OvertakeOffset,
                input.MaximumOffset,
                CommittedSide,
                out var passOffset);
            var followSpeed = AiRacecraftRules.CalculateFollowSpeed(traffic.AheadGap, traffic.AheadSpeed, input.Speed, input.FollowTimeGap);

            if (timeToContact < AvoidTimeToContact && traffic.AheadGap < AvoidGap)
            {
                if (!canPass)
                {
                    return new AiTacticalDecision(AiDrivingState.Avoiding, _offset, traffic.AheadSpeed * BlockedSpeedFactor);
                }

                StartPass(passOffset);
                return new AiTacticalDecision(AiDrivingState.Avoiding, _offset, Mathf.Max(followSpeed, AiRacecraftRules.PassingSpeed));
            }

            // A stopped or crawling car is an obstacle every driver goes around, whatever its temperament.
            var blockedBySlowCar = traffic.AheadSpeed < AiRacecraftRules.SlowCarSpeed;
            var wantsToPass = blockedBySlowCar
                || (AiRacecraftRules.IsPassingZone(input.FreeSpeed, input.UpcomingAbsoluteHeadingChange)
                    && AiRacecraftRules.ShouldAttemptOvertake(input.FreeSpeed, traffic.AheadSpeed, traffic.AheadGap, input.Aggression));
            if (canPass && wantsToPass)
            {
                StartPass(passOffset);
                // Still overlapping while moving across: keep closing, but at a speed that allows a nudge, not a ram.
                return new AiTacticalDecision(AiDrivingState.Overtaking, _offset, Mathf.Max(followSpeed, AiRacecraftRules.PassingSpeed));
            }

            return new AiTacticalDecision(AiDrivingState.Following, _offset, followSpeed);
        }

        /// <summary>+1 or -1 while a pass is under way on that side of the car ahead; 0 otherwise.</summary>
        private int CommittedSide => !_overtaking || Mathf.Approximately(_offset, 0f) ? 0 : (_offset > 0f ? 1 : -1);

        private void StartPass(float offset)
        {
            _offset = offset;
            _overtaking = true;
        }
    }
}
