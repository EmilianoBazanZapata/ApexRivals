using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Draws a closed track outline from normalized (0-1) points with a checkered start/finish marker at
    /// the first point. Mesh geometry keeps it crisp at any resolution without baked sprites.
    /// </summary>
    [AddComponentMenu("Apex Rivals/UI/Track Minimap Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrackMinimapGraphic : MaskableGraphic
    {
        private const int MarkerColumns = 4;
        private const int MarkerRows = 2;
        private const float MaximumMiterScale = 3f;

        [SerializeField, Min(0.5f)] private float lineThickness = 6f;
        [SerializeField, Min(0f)] private float markerCellSize = 5f;
        [SerializeField] private Color markerColor = Color.white;
        [SerializeField] private Color markerAlternateColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Vector2[] points = Array.Empty<Vector2>();

        public Color MarkerColor
        {
            get => markerColor;
            set
            {
                markerColor = value;
                SetVerticesDirty();
            }
        }

        public Color MarkerAlternateColor
        {
            get => markerAlternateColor;
            set
            {
                markerAlternateColor = value;
                SetVerticesDirty();
            }
        }

        public void SetPoints(IReadOnlyList<Vector2> normalizedPoints)
        {
            var count = normalizedPoints != null ? normalizedPoints.Count : 0;
            if (count == points.Length)
            {
                var unchanged = true;
                for (var index = 0; index < count && unchanged; index++)
                {
                    unchanged = points[index] == normalizedPoints[index];
                }

                if (unchanged)
                {
                    return;
                }
            }

            points = new Vector2[count];
            for (var index = 0; index < count; index++)
            {
                points[index] = normalizedPoints[index];
            }

            SetVerticesDirty();
        }

        /// <summary>
        /// Local (rect) position of a point given in the same normalized space as the outline, so markers such as
        /// cars line up with the drawn track. False until the outline has at least three points.
        /// </summary>
        public bool TryMapNormalized(Vector2 normalized, out Vector2 local)
        {
            if (!TryGetLayout(out var layout))
            {
                local = Vector2.zero;
                return false;
            }

            local = layout.Map(normalized);
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!TryGetLayout(out var layout))
            {
                return;
            }

            var mapped = new Vector2[points.Length];
            for (var index = 0; index < points.Length; index++)
            {
                mapped[index] = layout.Map(points[index]);
            }

            AddOutline(vertexHelper, mapped);
            AddStartMarker(vertexHelper, mapped);
        }

        private bool TryGetLayout(out Layout layout)
        {
            layout = default;
            if (points == null || points.Length < 3)
            {
                return false;
            }

            var rect = GetPixelAdjustedRect();
            var padding = Mathf.Max(lineThickness, markerCellSize * MarkerColumns * 0.5f) + 1f;
            var min = points[0];
            var max = points[0];
            for (var index = 1; index < points.Length; index++)
            {
                min = Vector2.Min(min, points[index]);
                max = Vector2.Max(max, points[index]);
            }

            // Turn the outline a quarter when that lets a wide track fill a tall box (or vice versa).
            var size = max - min;
            var rotate = size.x > size.y != rect.width > rect.height;
            if (rotate)
            {
                size = new Vector2(size.y, size.x);
            }

            var available = new Vector2(Mathf.Max(0f, rect.width - padding * 2f), Mathf.Max(0f, rect.height - padding * 2f));
            var scale = Mathf.Min(
                size.x > Mathf.Epsilon ? available.x / size.x : float.MaxValue,
                size.y > Mathf.Epsilon ? available.y / size.y : float.MaxValue);
            if (scale == float.MaxValue)
            {
                scale = 0f;
            }

            layout = new Layout(rect.center, (min + max) * 0.5f, scale, rotate);
            return true;
        }

        private readonly struct Layout
        {
            private readonly Vector2 _rectCentre;
            private readonly Vector2 _pointsCentre;
            private readonly float _scale;
            private readonly bool _rotate;

            public Layout(Vector2 rectCentre, Vector2 pointsCentre, float scale, bool rotate)
            {
                _rectCentre = rectCentre;
                _pointsCentre = pointsCentre;
                _scale = scale;
                _rotate = rotate;
            }

            public Vector2 Map(Vector2 point)
            {
                var local = point - _pointsCentre;
                if (_rotate)
                {
                    local = new Vector2(-local.y, local.x);
                }

                return _rectCentre + local * _scale;
            }
        }

        private void AddOutline(VertexHelper vertexHelper, Vector2[] mapped)
        {
            var count = mapped.Length;
            var halfThickness = lineThickness * 0.5f;
            for (var index = 0; index < count; index++)
            {
                var previous = mapped[(index - 1 + count) % count];
                var current = mapped[index];
                var next = mapped[(index + 1) % count];
                var inNormal = Normal(current - previous);
                var outNormal = Normal(next - current);
                var miter = (inNormal + outNormal).normalized;
                if (miter.sqrMagnitude < 0.5f)
                {
                    miter = outNormal;
                }

                var scale = Mathf.Min(MaximumMiterScale, 1f / Mathf.Max(0.2f, Vector2.Dot(miter, outNormal)));
                var offset = miter * halfThickness * scale;
                AddVertex(vertexHelper, current + offset, color);
                AddVertex(vertexHelper, current - offset, color);
            }

            for (var index = 0; index < count; index++)
            {
                var a = index * 2;
                var b = (index + 1) % count * 2;
                vertexHelper.AddTriangle(a, b, b + 1);
                vertexHelper.AddTriangle(a, b + 1, a + 1);
            }
        }

        private void AddStartMarker(VertexHelper vertexHelper, Vector2[] mapped)
        {
            if (markerCellSize <= 0f)
            {
                return;
            }

            var start = mapped[0];
            var along = (mapped[1] - mapped[mapped.Length - 1]).normalized;
            if (along.sqrMagnitude < 0.5f)
            {
                along = Vector2.right;
            }

            var across = Normal(along);
            var width = markerCellSize * MarkerColumns;
            var depth = markerCellSize * MarkerRows;
            var corner = start - across * (width * 0.5f) - along * (depth * 0.5f);
            for (var row = 0; row < MarkerRows; row++)
            {
                for (var column = 0; column < MarkerColumns; column++)
                {
                    var cellColor = (row + column) % 2 == 0 ? markerColor : markerAlternateColor;
                    var cellOrigin = corner + across * (column * markerCellSize) + along * (row * markerCellSize);
                    var first = vertexHelper.currentVertCount;
                    AddVertex(vertexHelper, cellOrigin, cellColor);
                    AddVertex(vertexHelper, cellOrigin + across * markerCellSize, cellColor);
                    AddVertex(vertexHelper, cellOrigin + across * markerCellSize + along * markerCellSize, cellColor);
                    AddVertex(vertexHelper, cellOrigin + along * markerCellSize, cellColor);
                    vertexHelper.AddTriangle(first, first + 1, first + 2);
                    vertexHelper.AddTriangle(first, first + 2, first + 3);
                }
            }
        }

        private static Vector2 Normal(Vector2 direction)
        {
            var normalized = direction.normalized;
            return new Vector2(-normalized.y, normalized.x);
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color vertexColor)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertexHelper.AddVert(vertex);
        }
    }
}
