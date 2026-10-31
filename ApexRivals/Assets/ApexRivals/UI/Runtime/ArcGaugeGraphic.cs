using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Ring arc drawn from <see cref="startAngle"/> to <see cref="endAngle"/> (degrees, 0 = right, counter-clockwise
    /// positive): a dark track, a fill up to <see cref="Value"/> that turns red past <see cref="RedZoneStart"/>, and
    /// optional major ticks outside the ring. With a full sweep and a thickness reaching the centre it draws a disk.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArcGaugeGraphic : MaskableGraphic
    {
        [SerializeField] private float startAngle = 210f;
        [SerializeField] private float endAngle = -30f;
        [SerializeField, Range(0f, 1f)] private float value;
        [SerializeField, Range(0f, 1f)] private float redZoneStart = 0.85f;
        [SerializeField, Min(0f)] private float thickness = 14f;
        [SerializeField, Min(0)] private int majorTicks = 8;
        [SerializeField, Min(0f)] private float tickLength = 14f;
        [SerializeField, Min(0f)] private float tickWidth = 3f;
        [SerializeField, Min(8)] private int segments = 72;
        [SerializeField] private Color trackColor = new Color32(0x2A, 0x2F, 0x36, 0xFF);
        [SerializeField] private Color redZoneTrackColor = new Color32(0x5A, 0x12, 0x10, 0xFF);
        [SerializeField] private Color fillColor = Color.white;
        [SerializeField] private Color redZoneColor = ApexUiVisualStyle.PrimaryRed;
        [SerializeField] private Color tickColor = Color.white;

        public float Value
        {
            get => value;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, this.value)) return;
                this.value = clamped;
                SetVerticesDirty();
            }
        }

        public float RedZoneStart
        {
            get => redZoneStart;
            set
            {
                redZoneStart = Mathf.Clamp01(value);
                SetVerticesDirty();
            }
        }

        public int MajorTicks
        {
            get => majorTicks;
            set
            {
                majorTicks = Mathf.Max(0, value);
                SetVerticesDirty();
            }
        }

        /// <summary>Local point on the ring at <paramref name="t"/> (0..1 along the sweep), offset outward by <paramref name="radiusOffset"/>.</summary>
        public Vector2 PointAt(float t, float radiusOffset)
        {
            var rect = GetPixelAdjustedRect();
            GetRadii(rect, out var outer, out var inner);
            return Polar(rect.center, (outer + inner) * 0.5f + radiusOffset, AngleAt(t));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            GetRadii(rect, out var outer, out var inner);
            if (outer <= 0f) return;

            var center = rect.center;
            var red = Mathf.Clamp01(redZoneStart);
            Band(vh, center, inner, outer, 0f, red, trackColor);
            Band(vh, center, inner, outer, red, 1f, redZoneTrackColor);
            Band(vh, center, inner, outer, 0f, Mathf.Min(value, red), fillColor);
            if (value > red) Band(vh, center, inner, outer, red, value, redZoneColor);

            for (var index = 0; index <= majorTicks && majorTicks > 0; index++)
            {
                var t = index / (float)majorTicks;
                Tick(vh, center, outer + 2f, outer + 2f + tickLength, AngleAt(t), t >= red ? redZoneColor : tickColor);
            }
        }

        private void GetRadii(Rect rect, out float outer, out float inner)
        {
            outer = Mathf.Min(rect.width, rect.height) * 0.5f - (majorTicks > 0 ? tickLength + 2f : 0f);
            inner = Mathf.Max(0f, outer - thickness);
        }

        private float AngleAt(float t) => Mathf.Lerp(startAngle, endAngle, Mathf.Clamp01(t)) * Mathf.Deg2Rad;

        private static Vector2 Polar(Vector2 center, float radius, float angle) => center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        private void Band(VertexHelper vh, Vector2 center, float inner, float outer, float from, float to, Color tint)
        {
            if (to <= from) return;
            var steps = Mathf.Max(1, Mathf.CeilToInt(segments * (to - from)));
            var tinted = (Color32)(tint * color);
            for (var step = 0; step < steps; step++)
            {
                var a0 = AngleAt(Mathf.Lerp(from, to, step / (float)steps));
                var a1 = AngleAt(Mathf.Lerp(from, to, (step + 1) / (float)steps));
                Quad(vh, tinted, Polar(center, inner, a0), Polar(center, outer, a0), Polar(center, outer, a1), Polar(center, inner, a1));
            }
        }

        private void Tick(VertexHelper vh, Vector2 center, float from, float to, float angle, Color tint)
        {
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-direction.y, direction.x) * (tickWidth * 0.5f);
            var a = center + direction * from;
            var b = center + direction * to;
            Quad(vh, (Color32)(tint * color), a - side, b - side, b + side, a + side);
        }

        private static void Quad(VertexHelper vh, Color32 tint, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var start = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero);
            vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero);
            vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
