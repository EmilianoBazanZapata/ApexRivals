using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// CPU trajectory sampling inspired by SGTP's predict/sample/select separation. This independent,
    /// bounded C# implementation retains the existing racecraft rules and uses constant-velocity rival
    /// forecasts, not SGTP's GPU MPPI solver or iterative multi-agent best response.
    /// </summary>
    public sealed class AiPredictivePlanner
    {
        private const float Step = 0.2f;
        private const int HorizonSteps = 12;
        private const float ForwardAcceleration = 3f;
        private const float LongitudinalClearance = 6f;
        private const float LateralClearance = AiRacecraftRules.LaneOverlapWidth;
        private readonly AiTacticalPlanner _racecraft = new AiTacticalPlanner();
        private readonly float[] _lanes = new float[7];
        private readonly float[] _targetSpeeds = new float[4];
        private int _laneCount;
        private float _previousOffset;
        private float _passOffset;

        public int EvaluatedCandidates { get; private set; }
        public int FeasibleCandidates { get; private set; }

        public void Reset()
        {
            _racecraft.Reset();
            _previousOffset = 0f;
            _passOffset = 0f;
            EvaluatedCandidates = 0;
            FeasibleCandidates = 0;
        }

        public AiTacticalDecision Decide(in AiTacticalInput input, IReadOnlyList<AiTrafficCar> cars,
            float lateralRate = 2.5f, float brakingDeceleration = 4.5f)
        {
            EvaluatedCandidates = 0;
            FeasibleCandidates = 0;
            var baseline = _racecraft.Decide(input, cars);
            if (cars == null || cars.Count == 0)
            {
                _passOffset = 0f;
                _previousOffset = baseline.LateralOffset;
                return baseline;
            }

            _laneCount = 0;
            var committed = Mathf.Abs(_passOffset) > 0.1f && !AiRacecraftRules.IsIdealLineClear(cars, 6f, 10f);
            if (!committed) _passOffset = 0f;
            AddLane(committed ? _passOffset : baseline.LateralOffset, input.MaximumOffset);
            if (!committed) AddLane(0f, input.MaximumOffset);
            if (!committed || Mathf.Abs(input.LateralOffset) > 0.1f && Mathf.Sign(input.LateralOffset) == Mathf.Sign(_passOffset))
                AddLane(input.LateralOffset, input.MaximumOffset);
            var traffic = AiRacecraftRules.Assess(input.CrossTrackError, input.Speed, cars);
            var canChangeLine = AiRacecraftRules.IsPassingZone(input.FreeSpeed, input.UpcomingAbsoluteHeadingChange)
                || (traffic.HasCarAhead && traffic.AheadSpeed < AiRacecraftRules.SlowCarSpeed);
            if (canChangeLine && input.Aggression >= 0.4f)
            {
                var centre = traffic.HasCarAhead ? traffic.AheadLateral : 0f;
                var pass = Mathf.Max(LateralClearance, input.OvertakeOffset);
                var left = centre - pass;
                var right = centre + pass;
                if (!committed || Mathf.Sign(left) == Mathf.Sign(_passOffset)) AddLane(left, input.MaximumOffset);
                if (!committed || Mathf.Sign(right) == Mathf.Sign(_passOffset)) AddLane(right, input.MaximumOffset);
            }

            _targetSpeeds[0] = Mathf.Max(0f, input.FreeSpeed);
            _targetSpeeds[1] = Mathf.Min(_targetSpeeds[0], baseline.TrafficSpeedLimit);
            _targetSpeeds[2] = Mathf.Min(_targetSpeeds[0], Mathf.Max(0f, input.Speed - 4f));
            _targetSpeeds[3] = 0f;
            var bestScore = float.NegativeInfinity;
            var bestOffset = baseline.LateralOffset;
            var bestSpeed = baseline.TrafficSpeedLimit;
            for (var lane = 0; lane < _laneCount; lane++)
            {
                for (var speed = 0; speed < _targetSpeeds.Length; speed++)
                {
                    EvaluatedCandidates++;
                    if (!Evaluate(input, cars, _lanes[lane], _targetSpeeds[speed], lateralRate,
                        brakingDeceleration, out var progress)) continue;
                    FeasibleCandidates++;
                    var score = progress - (committed ? 1.5f : 0.1f) * Mathf.Abs(_lanes[lane] - _previousOffset)
                        - 0.15f * Mathf.Abs(_lanes[lane]) - 0.5f * Mathf.Abs(_lanes[lane] - baseline.LateralOffset);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestOffset = _lanes[lane];
                    bestSpeed = _targetSpeeds[speed];
                }
            }

            if (FeasibleCandidates == 0)
            {
                // No sampled manoeuvre is safe: retain the established avoidance line and brake.
                _previousOffset = baseline.LateralOffset;
                return new AiTacticalDecision(AiDrivingState.Avoiding, committed ? _passOffset : baseline.LateralOffset, 0f);
            }

            _previousOffset = bestOffset;
            var passing = committed || traffic.HasCarAhead && Mathf.Abs(bestOffset - traffic.AheadLateral) >= LateralClearance;
            if (passing) _passOffset = bestOffset;
            var following = bestSpeed < input.FreeSpeed - 0.5f;
            var state = passing ? AiDrivingState.Overtaking : following ? AiDrivingState.Following : baseline.State;
            // Removing a traffic cap allows the driving controller to enforce its own corner/prop limits.
            var speedLimit = bestSpeed >= input.FreeSpeed - 0.01f ? float.PositiveInfinity : bestSpeed;
            return new AiTacticalDecision(state, bestOffset, speedLimit);
        }

        private void AddLane(float offset, float maximum)
        {
            if (Mathf.Abs(offset) > Mathf.Max(0f, maximum) + 0.001f) return;
            for (var index = 0; index < _laneCount; index++) if (Mathf.Abs(_lanes[index] - offset) < 0.01f) return;
            if (_laneCount < _lanes.Length) _lanes[_laneCount++] = offset;
        }

        private static bool Evaluate(in AiTacticalInput input, IReadOnlyList<AiTrafficCar> cars,
            float targetLateral, float targetSpeed, float lateralRate, float deceleration, out float progress)
        {
            progress = 0f;
            var lateral = input.CrossTrackError;
            var speed = Mathf.Max(0f, input.Speed);
            for (var step = 0; step < HorizonSteps; step++)
            {
                var nextSpeed = Mathf.MoveTowards(speed, targetSpeed,
                    (targetSpeed < speed ? Mathf.Max(0.1f, deceleration) : ForwardAcceleration) * Step);
                var nextProgress = progress + (speed + nextSpeed) * 0.5f * Step;
                var nextLateral = Mathf.MoveTowards(lateral, targetLateral, Mathf.Max(0f, lateralRate) * Step);
                var time = step * Step;
                for (var index = 0; index < cars.Count; index++)
                {
                    var car = cars[index];
                    var rivalLateralSpeed = Mathf.Clamp(car.LateralSpeed, -3f, 3f);
                    var from = new Vector2((car.Gap + car.Speed * time - progress) / LongitudinalClearance,
                        (car.Lateral + rivalLateralSpeed * time - lateral) / LateralClearance);
                    var to = new Vector2((car.Gap + car.Speed * (time + Step) - nextProgress) / LongitudinalClearance,
                        (car.Lateral + rivalLateralSpeed * (time + Step) - nextLateral) / LateralClearance);
                    // Swept separation catches cars crossing between prediction samples.
                    var delta = to - from;
                    var fraction = delta.sqrMagnitude > 0.00001f ? Mathf.Clamp01(-Vector2.Dot(from, delta) / delta.sqrMagnitude) : 0f;
                    if ((from + fraction * delta).sqrMagnitude < 1f) return false;
                }
                progress = nextProgress;
                lateral = nextLateral;
                speed = nextSpeed;
            }
            return true;
        }
    }
}
