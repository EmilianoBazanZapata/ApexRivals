using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// A dense, immutable-at-runtime representation derived from the one authored RacingLine.
    /// It deliberately has no alternate route or branch graph.
    /// </summary>
    public sealed class IdealRacingPath
    {
        public readonly struct Sample
        {
            public Sample(Vector3 position, Vector3 forward, float distanceAlongLap, float curvature, float targetSpeed)
            {
                Position = position;
                Forward = forward;
                DistanceAlongLap = distanceAlongLap;
                Curvature = curvature;
                TargetSpeed = targetSpeed;
            }

            public Vector3 Position { get; }
            public Vector3 Forward { get; }
            public float DistanceAlongLap { get; }
            public float Curvature { get; }
            public float TargetSpeed { get; }
        }

        public readonly struct Projection
        {
            public Projection(int segmentIndex, float segmentInterpolation, Vector3 position, Vector3 tangent, float distanceAlongLap, float crossTrackError, float distance)
            {
                SegmentIndex = segmentIndex;
                SegmentInterpolation = segmentInterpolation;
                Position = position;
                Tangent = tangent;
                DistanceAlongLap = distanceAlongLap;
                CrossTrackError = crossTrackError;
                Distance = distance;
            }

            public int SegmentIndex { get; }
            public float SegmentInterpolation { get; }
            public Vector3 Position { get; }
            public Vector3 Tangent { get; }
            public float DistanceAlongLap { get; }
            public float CrossTrackError { get; }
            public float Distance { get; }
        }

        private readonly Vector3[] _positions;
        private readonly Vector3[] _forwards;
        private readonly float[] _cumulativeDistances;
        private readonly float[] _curvatures;
        private readonly float[] _targetSpeeds;
        private readonly float[] _segmentLengths;

        private IdealRacingPath(Vector3[] positions, float targetSpeed, float minimumCornerSpeed, float curvatureSpeedScale)
        {
            _positions = positions;
            _forwards = new Vector3[positions.Length];
            _cumulativeDistances = new float[positions.Length];
            _curvatures = new float[positions.Length];
            _targetSpeeds = new float[positions.Length];
            _segmentLengths = new float[positions.Length];

            var distance = 0f;
            for (var index = 0; index < positions.Length; index++)
            {
                _cumulativeDistances[index] = distance;
                var nextIndex = IdealRacingDriverRules.WrapIndex(index + 1, positions.Length);
                var segment = positions[nextIndex] - positions[index];
                _segmentLengths[index] = segment.magnitude;
                distance += _segmentLengths[index];
                var planarSegment = Vector3.ProjectOnPlane(segment, Vector3.up);
                _forwards[index] = planarSegment.sqrMagnitude > 0.0001f ? planarSegment.normalized : Vector3.forward;
            }

            LapLength = distance;
            for (var index = 0; index < positions.Length; index++)
            {
                var nextIndex = IdealRacingDriverRules.WrapIndex(index + 1, positions.Length);
                var turnRadians = Vector3.Angle(_forwards[index], _forwards[nextIndex]) * Mathf.Deg2Rad;
                var localLength = Mathf.Max(0.01f, (_segmentLengths[index] + _segmentLengths[nextIndex]) * 0.5f);
                _curvatures[index] = turnRadians / localLength;
                _targetSpeeds[index] = IdealRacingDriverRules.CalculateProfileSpeed(
                    targetSpeed,
                    minimumCornerSpeed,
                    _curvatures[index],
                    curvatureSpeedScale,
                    1f);
            }
        }

        public int SampleCount => _positions.Length;
        public float LapLength { get; }
        public float AverageSpacing => SampleCount > 0 ? LapLength / SampleCount : 0f;

        public Sample GetSample(int index)
        {
            var wrapped = IdealRacingDriverRules.WrapIndex(index, SampleCount);
            return new Sample(_positions[wrapped], _forwards[wrapped], _cumulativeDistances[wrapped], _curvatures[wrapped], _targetSpeeds[wrapped]);
        }

        public static bool TryBuild(RacingLine source, float sampleSpacing, float targetSpeed, float minimumCornerSpeed, float curvatureSpeedScale, out IdealRacingPath path)
        {
            path = null;
            if (source == null || !source.IsValid)
            {
                return false;
            }

            var sourceCount = source.WaypointCount;
            var spacing = Mathf.Clamp(sampleSpacing, 2f, 5f);
            var count = 0;
            for (var index = 0; index < sourceCount; index++)
            {
                var length = Vector3.Distance(source.GetWaypointPosition(index), source.GetWaypointPosition(index + 1));
                count += Mathf.Max(1, Mathf.CeilToInt(length / spacing));
            }

            if (count < 3)
            {
                return false;
            }

            var positions = new Vector3[count];
            var writeIndex = 0;
            for (var index = 0; index < sourceCount; index++)
            {
                var start = source.GetWaypointPosition(index);
                var end = source.GetWaypointPosition(index + 1);
                var subdivisions = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / spacing));
                for (var subdivision = 0; subdivision < subdivisions; subdivision++)
                {
                    positions[writeIndex++] = Vector3.LerpUnclamped(start, end, subdivision / (float)subdivisions);
                }
            }

            path = new IdealRacingPath(positions, targetSpeed, minimumCornerSpeed, curvatureSpeedScale);
            return true;
        }

        public int FindNearestSegment(Vector3 position)
        {
            var nearestIndex = 0;
            var nearestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < SampleCount; index++)
            {
                EvaluateSegment(position, index, out _, out _, out var distanceSquared, out _);
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestIndex = index;
                }
            }

            return nearestIndex;
        }

        public bool TryProjectLocal(Vector3 position, int anchorIndex, int backwardWindow, int forwardWindow, out Projection projection)
        {
            projection = default;
            if (SampleCount < 3)
            {
                return false;
            }

            var nearestDistanceSquared = float.PositiveInfinity;
            var bestIndex = -1;
            var bestInterpolation = 0f;
            var bestPoint = Vector3.zero;
            var bestTangent = Vector3.forward;
            for (var offset = -Mathf.Max(0, backwardWindow); offset <= Mathf.Max(0, forwardWindow); offset++)
            {
                var index = IdealRacingDriverRules.WrapIndex(anchorIndex + offset, SampleCount);
                EvaluateSegment(position, index, out var interpolation, out var point, out var distanceSquared, out var tangent);
                if (distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                bestIndex = index;
                bestInterpolation = interpolation;
                bestPoint = point;
                bestTangent = tangent;
            }

            if (bestIndex < 0)
            {
                return false;
            }

            var right = Vector3.Cross(Vector3.up, bestTangent).normalized;
            var crossTrackError = Vector3.Dot(position - bestPoint, right);
            var distanceAlongLap = _cumulativeDistances[bestIndex] + _segmentLengths[bestIndex] * bestInterpolation;
            projection = new Projection(bestIndex, bestInterpolation, bestPoint, bestTangent, distanceAlongLap, crossTrackError, Mathf.Sqrt(nearestDistanceSquared));
            return true;
        }

        public Vector3 GetPointAhead(Projection projection, float arcDistance, out int targetSampleIndex, out float actualArcDistance)
        {
            var remaining = Mathf.Max(0f, arcDistance);
            var index = projection.SegmentIndex;
            var interpolation = projection.SegmentInterpolation;
            actualArcDistance = 0f;
            for (var traversed = 0; traversed < SampleCount; traversed++)
            {
                var start = _positions[index];
                var end = _positions[IdealRacingDriverRules.WrapIndex(index + 1, SampleCount)];
                var segmentLength = _segmentLengths[index];
                var available = segmentLength * (1f - interpolation);
                if (remaining <= available)
                {
                    targetSampleIndex = index;
                    actualArcDistance += remaining;
                    return Vector3.LerpUnclamped(start, end, interpolation + remaining / Mathf.Max(0.0001f, segmentLength));
                }

                remaining -= available;
                actualArcDistance += available;
                index = IdealRacingDriverRules.WrapIndex(index + 1, SampleCount);
                interpolation = 0f;
            }

            targetSampleIndex = index;
            return _positions[index];
        }

        /// <summary>
        /// Selects a real sequential sample rather than a point interpolated inside a segment.
        /// This is intentionally used for persistent steering targets: once selected, the sample
        /// index is stable until the driver reaches or passes it.
        /// </summary>
        public int GetForwardSampleAtArcDistance(Projection projection, float minimumArcDistance, out float actualArcDistance)
        {
            var requiredDistance = Mathf.Max(0f, minimumArcDistance);
            var segmentIndex = projection.SegmentIndex;
            var interpolation = projection.SegmentInterpolation;
            actualArcDistance = 0f;

            for (var traversed = 0; traversed < SampleCount; traversed++)
            {
                var availableDistance = _segmentLengths[segmentIndex] * (1f - interpolation);
                actualArcDistance += availableDistance;
                var sampleIndex = IdealRacingDriverRules.WrapIndex(segmentIndex + 1, SampleCount);
                if (actualArcDistance + 0.0001f >= requiredDistance)
                {
                    return sampleIndex;
                }

                segmentIndex = sampleIndex;
                interpolation = 0f;
            }

            return IdealRacingDriverRules.WrapIndex(segmentIndex + 1, SampleCount);
        }

        public float GetArcDistanceToSample(Projection projection, int targetSampleIndex)
        {
            var targetIndex = IdealRacingDriverRules.WrapIndex(targetSampleIndex, SampleCount);
            var segmentIndex = projection.SegmentIndex;
            var interpolation = projection.SegmentInterpolation;
            var distance = 0f;

            for (var traversed = 0; traversed < SampleCount; traversed++)
            {
                distance += _segmentLengths[segmentIndex] * (1f - interpolation);
                if (IdealRacingDriverRules.WrapIndex(segmentIndex + 1, SampleCount) == targetIndex)
                {
                    return distance;
                }

                segmentIndex = IdealRacingDriverRules.WrapIndex(segmentIndex + 1, SampleCount);
                interpolation = 0f;
            }

            return LapLength;
        }

        public float GetMinimumTargetSpeedAhead(Projection projection, float previewDistance)
        {
            var remaining = Mathf.Max(0f, previewDistance);
            var index = projection.SegmentIndex;
            var interpolation = projection.SegmentInterpolation;
            var minimum = _targetSpeeds[index];
            for (var traversed = 0; traversed < SampleCount && remaining > 0f; traversed++)
            {
                minimum = Mathf.Min(minimum, _targetSpeeds[index]);
                remaining -= _segmentLengths[index] * (1f - interpolation);
                index = IdealRacingDriverRules.WrapIndex(index + 1, SampleCount);
                interpolation = 0f;
            }

            return minimum;
        }

        private void EvaluateSegment(Vector3 position, int index, out float interpolation, out Vector3 point, out float distanceSquared, out Vector3 tangent)
        {
            var wrapped = IdealRacingDriverRules.WrapIndex(index, SampleCount);
            var start = _positions[wrapped];
            var end = _positions[IdealRacingDriverRules.WrapIndex(wrapped + 1, SampleCount)];
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            interpolation = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector3.Dot(position - start, segment) / lengthSquared) : 0f;
            point = start + segment * interpolation;
            distanceSquared = (position - point).sqrMagnitude;
            tangent = _forwards[wrapped];
        }
    }
}
