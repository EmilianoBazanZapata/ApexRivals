using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>A lap-local map of corners and a continuous, braking-feasible speed envelope.</summary>
    public sealed class AiCircuitPlan
    {
        public readonly struct Corner
        {
            public Corner(float entry, float apex, float exit, float angle, float radius, float speed)
            {
                EntryDistance = entry;
                ApexDistance = apex;
                ExitDistance = exit;
                SignedAngle = angle;
                Radius = radius;
                ApexSpeed = speed;
            }

            public float EntryDistance { get; }
            public float ApexDistance { get; }
            public float ExitDistance { get; }
            public float SignedAngle { get; }
            public float Radius { get; }
            public float ApexSpeed { get; }
            public float Direction => Mathf.Sign(SignedAngle);
        }

        private const float GeometryHalfWindow = 15f;
        private const float MinimumCornerAngle = 12f;
        private readonly IdealRacingPath _path;
        private readonly float[] _curvature;
        private readonly float[] _speeds;
        private readonly float[] _brakingSpeeds;
        private readonly float[] _lengths;
        private readonly Corner[] _corners;
        private float _cachedDeceleration = -1f;
        private float _cachedFactor = -1f;

        private AiCircuitPlan(IdealRacingPath path, float maximumSpeed, float grip)
        {
            _path = path;
            var count = path.SampleCount;
            _curvature = new float[count];
            _speeds = new float[count];
            _brakingSpeeds = new float[count];
            _lengths = new float[count];
            var rawTurns = new float[count];
            var directions = new int[count];
            var halfWindow = Mathf.Max(1, Mathf.RoundToInt(GeometryHalfWindow / Mathf.Max(0.1f, path.AverageSpacing)));
            for (var index = 0; index < count; index++)
            {
                var sample = path.GetSample(index);
                var next = path.GetSample(index + 1);
                _lengths[index] = Vector3.Distance(sample.Position, next.Position);
                rawTurns[index] = Vector3.SignedAngle(sample.Forward, next.Forward, Vector3.up);
            }
            for (var index = 0; index < count; index++)
            {
                var window = 0f;
                var absoluteTurn = 0f;
                for (var offset = -halfWindow; offset < halfWindow; offset++)
                {
                    var wrapped = IdealRacingDriverRules.WrapIndex(index + offset, count);
                    window += _lengths[wrapped];
                    absoluteTurn += Mathf.Abs(rawTurns[wrapped]);
                }

                // Opposite bends must not cancel into an unsafe straight-line speed through a chicane.
                var magnitude = absoluteTurn * Mathf.Deg2Rad / Mathf.Max(0.01f, window);
                _speeds[index] = magnitude > 0.0001f && grip > 0f && !float.IsInfinity(grip)
                    ? Mathf.Min(maximumSpeed, Mathf.Max(5f, Mathf.Sqrt(grip / magnitude)))
                    : maximumSpeed;
                var localTurn = rawTurns[IdealRacingDriverRules.WrapIndex(index - 1, count)]
                    + 2f * rawTurns[index] + rawTurns[(index + 1) % count];
                _curvature[index] = Mathf.Abs(localTurn) > 0.001f ? Mathf.Sign(localTurn) * magnitude : 0f;
                directions[index] = Mathf.Abs(localTurn) >= 1f ? (localTurn > 0f ? 1 : -1) : 0;
            }

            // Start at a boundary so a corner straddling the finish line remains a single corner.
            var first = 0;
            for (var index = 0; index < count; index++)
            {
                if (directions[index] == directions[IdealRacingDriverRules.WrapIndex(index - 1, count)]) continue;
                first = index;
                break;
            }

            var corners = new List<Corner>();
            for (var traversed = 0; traversed < count;)
            {
                var start = traversed;
                var direction = directions[(first + start) % count];
                var angle = 0f;
                do
                {
                    angle += rawTurns[(first + traversed) % count];
                    traversed++;
                } while (traversed < count && directions[(first + traversed) % count] == direction);
                if (direction == 0 || Mathf.Abs(angle) < MinimumCornerAngle) continue;

                var apex = (first + (start + traversed - 1) / 2) % count;
                for (var offset = start; offset < traversed; offset++)
                {
                    var candidate = (first + offset) % count;
                    if (Mathf.Abs(_curvature[candidate]) > Mathf.Abs(_curvature[apex]) + 0.0001f) apex = candidate;
                }

                corners.Add(new Corner(path.GetSample(first + start).DistanceAlongLap,
                    path.GetSample(apex).DistanceAlongLap, path.GetSample(first + traversed).DistanceAlongLap,
                    angle, 1f / Mathf.Max(0.0001f, Mathf.Abs(_curvature[apex])), _speeds[apex]));
            }
            _corners = corners.ToArray();
        }

        public IReadOnlyList<Corner> Corners => _corners;

        public static AiCircuitPlan Build(IdealRacingPath path, float maximumSpeed, float grip)
        {
            if (path == null || path.SampleCount < 3 || maximumSpeed <= 0f) return null;
            return new AiCircuitPlan(path, maximumSpeed, grip);
        }

        public bool TryGetCorner(float distanceAlongLap, float preview, out int cornerIndex, out Corner corner, out float entryGap)
        {
            cornerIndex = -1;
            corner = default;
            entryGap = float.PositiveInfinity;
            for (var index = 0; index < _corners.Length; index++)
            {
                var candidate = _corners[index];
                var length = Mathf.Repeat(candidate.ExitDistance - candidate.EntryDistance, _path.LapLength);
                if (length < 0.001f) length = _path.LapLength;
                var progress = Mathf.Repeat(distanceAlongLap - candidate.EntryDistance, _path.LapLength);
                var gap = progress < length ? 0f : Mathf.Repeat(candidate.EntryDistance - distanceAlongLap, _path.LapLength);
                if (gap > Mathf.Max(0f, preview) || gap >= entryGap) continue;
                cornerIndex = index;
                corner = candidate;
                entryGap = gap;
            }
            return cornerIndex >= 0;
        }

        public bool IsBeforeApex(float distanceAlongLap, in Corner corner)
        {
            var progress = Mathf.Repeat(distanceAlongLap - corner.EntryDistance, _path.LapLength);
            var length = Mathf.Repeat(corner.ExitDistance - corner.EntryDistance, _path.LapLength);
            if (length < 0.001f) length = _path.LapLength;
            return progress >= length || progress <= Mathf.Repeat(corner.ApexDistance - corner.EntryDistance, _path.LapLength);
        }

        public float GetSpeed(IdealRacingPath.Projection projection, float deceleration, float factor)
        {
            var safeDeceleration = Mathf.Max(0.1f, deceleration);
            var safeFactor = Mathf.Max(0f, factor);
            if (!Mathf.Approximately(_cachedDeceleration, safeDeceleration) || !Mathf.Approximately(_cachedFactor, safeFactor))
            {
                _cachedDeceleration = safeDeceleration;
                _cachedFactor = safeFactor;
                for (var index = 0; index < _speeds.Length; index++) _brakingSpeeds[index] = _speeds[index] * safeFactor;
                // Two laps propagate every braking constraint across the finish seam.
                for (var step = _speeds.Length * 2 - 1; step >= 0; step--)
                {
                    var index = step % _speeds.Length;
                    var next = (index + 1) % _speeds.Length;
                    _brakingSpeeds[index] = Mathf.Min(_brakingSpeeds[index],
                        Mathf.Sqrt(_brakingSpeeds[next] * _brakingSpeeds[next] + 2f * safeDeceleration * _lengths[index]));
                }
            }

            var current = projection.SegmentIndex;
            var following = (current + 1) % _speeds.Length;
            var fraction = Mathf.Clamp01(projection.SegmentInterpolation);
            var localLimit = Mathf.Lerp(_speeds[current], _speeds[following], fraction) * safeFactor;
            var distance = _lengths[current] * (1f - fraction);
            var brakingLimit = Mathf.Sqrt(_brakingSpeeds[following] * _brakingSpeeds[following] + 2f * safeDeceleration * distance);
            return Mathf.Min(localLimit, brakingLimit);
        }
    }
}
