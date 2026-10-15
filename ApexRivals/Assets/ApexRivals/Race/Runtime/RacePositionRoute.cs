using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    /// <summary>Projects racers only onto the route between their last and next valid checkpoints.</summary>
    public sealed class RacePositionRoute
    {
        private readonly Vector3[][] _sectors;

        public RacePositionRoute(IReadOnlyList<Vector3> checkpoints, IReadOnlyList<Vector3> loop)
        {
            if (checkpoints == null || checkpoints.Count < 2 || loop == null || loop.Count < 2)
                return;

            var distances = new float[loop.Count + 1];
            for (var i = 0; i < loop.Count; i++)
                distances[i + 1] = distances[i] + Vector3.Distance(Planar(loop[i]), Planar(loop[(i + 1) % loop.Count]));
            var length = distances[loop.Count];
            if (length < 0.001f) return;

            var checkpointArcs = new float[checkpoints.Count];
            for (var i = 0; i < checkpoints.Count; i++)
                checkpointArcs[i] = ProjectArc(checkpoints[i], loop, distances);

            // Checkpoints must cover the supplied loop once in forward order.
            var coveredLength = 0f;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var span = Mathf.Repeat(checkpointArcs[(i + 1) % checkpoints.Count] - checkpointArcs[i], length);
                if (span < 0.001f) return;
                coveredLength += span;
            }
            if (Mathf.Abs(coveredLength - length) > 0.1f) return;

            _sectors = new Vector3[checkpoints.Count][];
            for (var next = 0; next < checkpoints.Count; next++)
            {
                var previous = (next + checkpoints.Count - 1) % checkpoints.Count;
                var start = checkpointArcs[previous];
                var span = Mathf.Repeat(checkpointArcs[next] - start, length);
                var points = new List<Vector3> { PointAt(start, loop, distances) };
                // Visit in arc order, including the finish-line wrap.
                for (var pass = 0; pass < 2; pass++)
                    for (var i = 0; i < loop.Count; i++)
                    {
                        var arc = distances[i] + pass * length;
                        if (arc > start && arc < start + span) points.Add(Planar(loop[i]));
                    }
                points.Add(PointAt(start + span, loop, distances));
                _sectors[next] = points.ToArray();
            }
        }

        public bool IsValid => _sectors != null;

        public float GetSectorProgress(int nextCheckpoint, Vector3 position)
        {
            if (_sectors == null || nextCheckpoint < 0 || nextCheckpoint >= _sectors.Length) return 0f;
            var points = _sectors[nextCheckpoint];
            var travelled = 0f;
            var nearestDistance = float.PositiveInfinity;
            var nearestArc = 0f;
            position = Planar(position);
            for (var i = 0; i < points.Length - 1; i++)
            {
                var delta = points[i + 1] - points[i];
                var t = delta.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(Vector3.Dot(position - points[i], delta) / delta.sqrMagnitude) : 0f;
                var distance = (position - points[i] - delta * t).sqrMagnitude;
                var segmentLength = delta.magnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestArc = travelled + segmentLength * t;
                }
                travelled += segmentLength;
            }
            return nearestArc;
        }

        private static float ProjectArc(Vector3 point, IReadOnlyList<Vector3> loop, float[] distances)
        {
            point = Planar(point);
            var closest = float.PositiveInfinity;
            var arc = 0f;
            for (var i = 0; i < loop.Count; i++)
            {
                var start = Planar(loop[i]);
                var delta = Planar(loop[(i + 1) % loop.Count]) - start;
                var t = delta.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(Vector3.Dot(point - start, delta) / delta.sqrMagnitude) : 0f;
                var distance = (point - start - delta * t).sqrMagnitude;
                if (distance < closest)
                {
                    closest = distance;
                    arc = distances[i] + delta.magnitude * t;
                }
            }
            return arc;
        }

        private static Vector3 PointAt(float arc, IReadOnlyList<Vector3> loop, float[] distances)
        {
            arc = Mathf.Repeat(arc, distances[loop.Count]);
            for (var i = 0; i < loop.Count; i++)
                if (arc <= distances[i + 1])
                {
                    var span = distances[i + 1] - distances[i];
                    return Vector3.Lerp(Planar(loop[i]), Planar(loop[(i + 1) % loop.Count]), span > 0f ? (arc - distances[i]) / span : 0f);
                }
            return Planar(loop[0]);
        }

        private static Vector3 Planar(Vector3 point) => new Vector3(point.x, 0f, point.z);
    }
}
