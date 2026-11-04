using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    public enum ApexUiIconShape
    {
        DoubleChevronRight,
        ChevronLeft,
        ChevronRight,
        Plus,
        Cross,
        Gear
    }

    /// <summary>
    /// Resolution-independent stroke icons for menu buttons and selectors. Drawn as mesh geometry so the
    /// icons do not depend on glyphs that the fallback font may not contain.
    /// </summary>
    [AddComponentMenu("Apex Rivals/UI/Icon Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ApexUiIconGraphic : MaskableGraphic
    {
        private const int GearTeeth = 8;
        private const int GearRingSegments = 32;

        [SerializeField] private ApexUiIconShape shape;
        [SerializeField, Range(0.04f, 0.4f)] private float strokeRatio = 0.14f;

        public ApexUiIconShape Shape
        {
            get => shape;
            set
            {
                shape = value;
                SetVerticesDirty();
            }
        }

        public float StrokeRatio
        {
            get => strokeRatio;
            set
            {
                strokeRatio = Mathf.Clamp(value, 0.04f, 0.4f);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            var rect = GetPixelAdjustedRect();
            var size = Mathf.Min(rect.width, rect.height);
            if (size <= 0f)
            {
                return;
            }

            var center = rect.center;
            var half = size * 0.5f;
            var stroke = size * strokeRatio;

            switch (shape)
            {
                case ApexUiIconShape.DoubleChevronRight:
                    AddChevron(vertexHelper, center + new Vector2(-half * 0.42f, 0f), half * 0.5f, half * 0.8f, stroke, 1f);
                    AddChevron(vertexHelper, center + new Vector2(half * 0.42f, 0f), half * 0.5f, half * 0.8f, stroke, 1f);
                    break;
                case ApexUiIconShape.ChevronLeft:
                    AddChevron(vertexHelper, center, half * 0.5f, half * 0.9f, stroke, -1f);
                    break;
                case ApexUiIconShape.ChevronRight:
                    AddChevron(vertexHelper, center, half * 0.5f, half * 0.9f, stroke, 1f);
                    break;
                case ApexUiIconShape.Plus:
                    AddSegment(vertexHelper, center + new Vector2(-half * 0.8f, 0f), center + new Vector2(half * 0.8f, 0f), stroke);
                    AddSegment(vertexHelper, center + new Vector2(0f, -half * 0.8f), center + new Vector2(0f, half * 0.8f), stroke);
                    break;
                case ApexUiIconShape.Cross:
                    AddSegment(vertexHelper, center + new Vector2(-half * 0.65f, -half * 0.65f), center + new Vector2(half * 0.65f, half * 0.65f), stroke);
                    AddSegment(vertexHelper, center + new Vector2(-half * 0.65f, half * 0.65f), center + new Vector2(half * 0.65f, -half * 0.65f), stroke);
                    break;
                case ApexUiIconShape.Gear:
                    AddGear(vertexHelper, center, half, stroke);
                    break;
            }
        }

        private void AddChevron(VertexHelper vertexHelper, Vector2 center, float halfWidth, float halfHeight, float stroke, float direction)
        {
            var tip = center + new Vector2(halfWidth * direction, 0f);
            AddSegment(vertexHelper, center + new Vector2(-halfWidth * direction, halfHeight), tip, stroke);
            AddSegment(vertexHelper, tip, center + new Vector2(-halfWidth * direction, -halfHeight), stroke);
        }

        private void AddGear(VertexHelper vertexHelper, Vector2 center, float half, float stroke)
        {
            var ringOuter = half * 0.62f;
            var ringInner = Mathf.Max(0f, ringOuter - stroke);
            AddRing(vertexHelper, center, ringInner, ringOuter);

            var toothInner = ringOuter - stroke * 0.25f;
            var toothOuter = half * 0.95f;
            var toothHalfWidth = stroke * 0.55f;
            for (var index = 0; index < GearTeeth; index++)
            {
                var angle = index * Mathf.PI * 2f / GearTeeth;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var normal = new Vector2(-direction.y, direction.x) * toothHalfWidth;
                var inner = center + direction * toothInner;
                var outer = center + direction * toothOuter;
                AddQuad(vertexHelper, inner - normal, inner + normal, outer + normal, outer - normal);
            }
        }

        private void AddRing(VertexHelper vertexHelper, Vector2 center, float innerRadius, float outerRadius)
        {
            for (var index = 0; index < GearRingSegments; index++)
            {
                var start = index * Mathf.PI * 2f / GearRingSegments;
                var end = (index + 1) * Mathf.PI * 2f / GearRingSegments;
                var startDirection = new Vector2(Mathf.Cos(start), Mathf.Sin(start));
                var endDirection = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
                AddQuad(
                    vertexHelper,
                    center + startDirection * innerRadius,
                    center + startDirection * outerRadius,
                    center + endDirection * outerRadius,
                    center + endDirection * innerRadius);
            }
        }

        private void AddSegment(VertexHelper vertexHelper, Vector2 from, Vector2 to, float stroke)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var direction = delta.normalized;
            // Extending each end by half the stroke squares off the joins where two segments meet.
            var extension = direction * (stroke * 0.5f);
            var normal = new Vector2(-direction.y, direction.x) * (stroke * 0.5f);
            var start = from - extension;
            var end = to + extension;
            AddQuad(vertexHelper, start - normal, start + normal, end + normal, end - normal);
        }

        private void AddQuad(VertexHelper vertexHelper, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var startIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, a);
            AddVertex(vertexHelper, b);
            AddVertex(vertexHelper, c);
            AddVertex(vertexHelper, d);
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        private void AddVertex(VertexHelper vertexHelper, Vector2 position)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vertexHelper.AddVert(vertex);
        }
    }
}
