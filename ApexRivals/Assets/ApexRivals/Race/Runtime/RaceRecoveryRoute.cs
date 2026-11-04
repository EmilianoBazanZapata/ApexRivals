using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    /// <summary>Three-dimensional recovery positions restricted to the racer's validated checkpoint sector.</summary>
    public sealed class RaceRecoveryRoute
    {
        private readonly Vector3[] _points;
        private readonly float[] _arcs;
        private readonly float[] _checkpointArcs;
        public bool IsValid => _checkpointArcs != null;

        public RaceRecoveryRoute(IReadOnlyList<Vector3> checkpoints, IReadOnlyList<Vector3> points)
        {
            if (checkpoints == null || checkpoints.Count < 2 || points == null || points.Count < 3) return;
            _points = new Vector3[points.Count];
            _arcs = new float[points.Count + 1];
            for (var i = 0; i < points.Count; i++)
            {
                _points[i] = points[i];
                _arcs[i + 1] = _arcs[i] + Vector3.Distance(points[i], points[(i + 1) % points.Count]);
            }
            var length = _arcs[points.Count];
            if (length < .01f) return;
            var checkpointArcs = new float[checkpoints.Count];
            for (var i = 0; i < checkpoints.Count; i++) checkpointArcs[i] = Project(checkpoints[i], 0, length, out _, out _);
            float covered = 0;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var span = Mathf.Repeat(checkpointArcs[(i + 1) % checkpoints.Count] - checkpointArcs[i], length);
                if (span < .01f) return;
                covered += span;
            }
            if (Mathf.Abs(covered - length) > .1f) return;
            _checkpointArcs = checkpointArcs;
        }

        public bool TryProject(int nextCheckpoint, Vector3 position, out Vector3 point, out Quaternion rotation, out float distance)
        {
            point = default; rotation = Quaternion.identity; distance = float.PositiveInfinity;
            if (!IsValid || nextCheckpoint < 0 || nextCheckpoint >= _checkpointArcs.Length) return false;
            var previous = (nextCheckpoint + _checkpointArcs.Length - 1) % _checkpointArcs.Length;
            var start = _checkpointArcs[previous];
            var length = _arcs[_points.Length];
            var span = Mathf.Repeat(_checkpointArcs[nextCheckpoint] - start, length);
            var arc = Project(position, start, start + span, out _, out distance);
            // Stay behind the next unaccepted trigger, including the start/finish wrap.
            arc = Mathf.Min(arc, start + Mathf.Max(0, span - 5));
            point = PointAt(arc, out var tangent);
            var forward = Vector3.ProjectOnPlane(tangent, Vector3.up);
            if (forward.sqrMagnitude < .0001f) return false;
            rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            return true;
        }

        public bool TryGetNearestTrackPoint(Vector3 position, out Vector3 point)
        {
            point = default;
            if (!IsValid) return false;
            Project(position, 0, _arcs[_points.Length], out point, out _);
            return true;
        }

        private float Project(Vector3 position, float start, float end, out Vector3 point, out float distance)
        {
            var bestArc = start;
            point = default;
            var bestDistance = float.PositiveInfinity;
            var length = _arcs[_points.Length];
            for (var pass = 0; pass < 2; pass++)
                for (var i = 0; i < _points.Length; i++)
                {
                    var segmentStart = _arcs[i] + pass * length;
                    var segmentEnd = _arcs[i + 1] + pass * length;
                    if (segmentEnd < start || segmentStart > end || segmentEnd - segmentStart < .0001f) continue;
                    var delta = _points[(i + 1) % _points.Length] - _points[i];
                    var low = Mathf.Clamp01((start - segmentStart) / (segmentEnd - segmentStart));
                    var high = Mathf.Clamp01((end - segmentStart) / (segmentEnd - segmentStart));
                    var t = Mathf.Clamp(Vector3.Dot(position - _points[i], delta) / delta.sqrMagnitude, low, high);
                    var candidate = _points[i] + delta * t;
                    var squared = (candidate - position).sqrMagnitude;
                    if (squared >= bestDistance) continue;
                    bestDistance = squared;
                    point = candidate;
                    bestArc = Mathf.Lerp(segmentStart, segmentEnd, t);
                }
            distance = Mathf.Sqrt(bestDistance);
            return bestArc;
        }

        private Vector3 PointAt(float arc, out Vector3 tangent)
        {
            arc = Mathf.Repeat(arc, _arcs[_points.Length]);
            for (var i = 0; i < _points.Length; i++)
            {
                if (arc > _arcs[i + 1] || _arcs[i + 1] - _arcs[i] < .0001f) continue;
                tangent = _points[(i + 1) % _points.Length] - _points[i];
                return Vector3.Lerp(_points[i], _points[(i + 1) % _points.Length], (arc - _arcs[i]) / (_arcs[i + 1] - _arcs[i]));
            }
            tangent = Vector3.forward;
            return _points[0];
        }
    }
}
