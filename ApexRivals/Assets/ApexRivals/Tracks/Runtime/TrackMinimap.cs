using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Tracks.Runtime
{
    /// <summary>
    /// Maps world x/z positions into the unit square that holds a track outline: the outline's bounds are fitted
    /// with the aspect preserved and centred, so cars mapped with the same frame land on the drawn track.
    /// </summary>
    public readonly struct TrackMinimapFrame
    {
        private readonly Vector2 _min;
        private readonly Vector2 _offset;
        private readonly float _scale;

        public TrackMinimapFrame(Vector2 min, Vector2 offset, float scale)
        {
            _min = min;
            _offset = offset;
            _scale = scale;
        }

        public bool IsValid => _scale > Mathf.Epsilon;

        public Vector2 ToNormalized(Vector2 world)
        {
            return IsValid ? (world - _min + _offset) / _scale : new Vector2(0.5f, 0.5f);
        }
    }

    /// <summary>Pure helpers that turn a world-space racing line into minimap data.</summary>
    public static class TrackMinimap
    {
        /// <summary>Length of the closed loop through <paramref name="points"/>.</summary>
        public static float ClosedLength(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2)
            {
                return 0f;
            }

            var length = 0f;
            for (var index = 0; index < points.Count; index++)
            {
                length += Vector2.Distance(points[index], points[(index + 1) % points.Count]);
            }

            return length;
        }

        /// <summary>The frame that fits <paramref name="points"/> into the unit square (aspect preserved, centred).</summary>
        public static TrackMinimapFrame Fit(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
            {
                return default;
            }

            var min = points[0];
            var max = points[0];
            for (var index = 1; index < points.Count; index++)
            {
                min = Vector2.Min(min, points[index]);
                max = Vector2.Max(max, points[index]);
            }

            var size = max - min;
            var scale = Mathf.Max(size.x, size.y);
            return new TrackMinimapFrame(min, new Vector2((scale - size.x) * 0.5f, (scale - size.y) * 0.5f), scale);
        }

        /// <summary>
        /// Fits the loop into the unit square (aspect preserved, centred) and rotates it so it starts at
        /// <paramref name="startIndex"/>, which becomes the start/finish marker.
        /// </summary>
        public static Vector2[] Normalize(IReadOnlyList<Vector2> points, int startIndex = 0)
        {
            if (points == null || points.Count == 0)
            {
                return new Vector2[0];
            }

            var frame = Fit(points);
            var count = points.Count;
            var start = (startIndex % count + count) % count;
            var normalized = new Vector2[count];
            for (var index = 0; index < count; index++)
            {
                normalized[index] = frame.ToNormalized(points[(start + index) % count]);
            }

            return normalized;
        }
    }
}
