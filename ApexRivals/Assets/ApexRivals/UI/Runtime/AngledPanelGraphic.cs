using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [AddComponentMenu("Apex Rivals/UI/Angled Panel Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AngledPanelGraphic : MaskableGraphic
    {
        [SerializeField] private float leftTopInset;
        [SerializeField] private float leftBottomInset;
        [SerializeField] private float rightTopInset;
        [SerializeField] private float rightBottomInset;
        [SerializeField, Min(0f)] private float borderThickness;
        [SerializeField] private Color borderColor = Color.clear;

        public float BorderThickness
        {
            get => borderThickness;
            set
            {
                borderThickness = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public Color BorderColor
        {
            get => borderColor;
            set
            {
                if (borderColor == value)
                {
                    return;
                }

                borderColor = value;
                SetVerticesDirty();
            }
        }

        public float LeftTopInset
        {
            get => leftTopInset;
            set
            {
                leftTopInset = value;
                SetVerticesDirty();
            }
        }

        public float LeftBottomInset
        {
            get => leftBottomInset;
            set
            {
                leftBottomInset = value;
                SetVerticesDirty();
            }
        }

        public float RightTopInset
        {
            get => rightTopInset;
            set
            {
                rightTopInset = value;
                SetVerticesDirty();
            }
        }

        public float RightBottomInset
        {
            get => rightBottomInset;
            set
            {
                rightBottomInset = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            var rect = GetPixelAdjustedRect();
            var left = rect.xMin;
            var right = rect.xMax;
            var top = rect.yMax;
            var bottom = rect.yMin;

            // Clockwise: left-bottom, left-top, right-top, right-bottom.
            var outer = new[]
            {
                new Vector2(left + leftBottomInset, bottom),
                new Vector2(left + leftTopInset, top),
                new Vector2(right - rightTopInset, top),
                new Vector2(right - rightBottomInset, bottom)
            };

            for (var index = 0; index < outer.Length; index++)
            {
                AddVertex(vertexHelper, outer[index], color);
            }

            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);

            if (borderThickness <= 0f || borderColor.a <= 0f)
            {
                return;
            }

            var inner = new Vector2[outer.Length];
            for (var index = 0; index < outer.Length; index++)
            {
                inner[index] = InsetCorner(outer, index, borderThickness);
            }

            // The border is drawn after the fill so it blends over it within the same mesh.
            for (var index = 0; index < outer.Length; index++)
            {
                var next = (index + 1) % outer.Length;
                var start = vertexHelper.currentVertCount;
                AddVertex(vertexHelper, outer[index], borderColor);
                AddVertex(vertexHelper, outer[next], borderColor);
                AddVertex(vertexHelper, inner[next], borderColor);
                AddVertex(vertexHelper, inner[index], borderColor);
                vertexHelper.AddTriangle(start, start + 1, start + 2);
                vertexHelper.AddTriangle(start + 2, start + 3, start);
            }
        }

        /// <summary>
        /// Intersects the two edges that meet at a corner after moving each one inward by the thickness.
        /// </summary>
        private static Vector2 InsetCorner(Vector2[] polygon, int index, float thickness)
        {
            var previous = polygon[(index + polygon.Length - 1) % polygon.Length];
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Length];

            var incoming = (current - previous).normalized;
            var outgoing = (next - current).normalized;
            var incomingPoint = previous + InwardNormal(incoming) * thickness;
            var outgoingPoint = current + InwardNormal(outgoing) * thickness;

            var denominator = Cross(incoming, outgoing);
            if (Mathf.Abs(denominator) < 0.0001f)
            {
                return current + InwardNormal(outgoing) * thickness;
            }

            var distance = Cross(outgoingPoint - incomingPoint, outgoing) / denominator;
            return incomingPoint + incoming * distance;
        }

        private static Vector2 InwardNormal(Vector2 direction)
        {
            return new Vector2(direction.y, -direction.x);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color vertexColor)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = vertexColor;
            vertex.position = position;
            vertexHelper.AddVert(vertex);
        }
    }
}
