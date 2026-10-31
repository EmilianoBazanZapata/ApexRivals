using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Audio.Runtime.Bench
{
    /// <summary>Draws a list of values as a line (oscilloscope, -1..1) or as bars (spectrum, 0..1).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SignalGraphic : MaskableGraphic
    {
        [SerializeField] private bool bars;
        [SerializeField, Min(0.5f)] private float lineWidth = 2f;
        [SerializeField, Min(0f)] private float barGap = 2f;

        private float[] _values = new float[0];

        public void SetValues(float[] values)
        {
            _values = values ?? new float[0];
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var count = _values.Length;
            if (count < 2 || rect.width <= 0f) return;

            if (bars)
            {
                var width = rect.width / count;
                for (var i = 0; i < count; i++)
                {
                    var height = Mathf.Clamp01(_values[i]) * rect.height;
                    var x = rect.xMin + i * width;
                    Quad(vh, new Vector2(x, rect.yMin), new Vector2(x + width - barGap, rect.yMin + height));
                }

                return;
            }

            var mid = rect.center.y;
            var half = rect.height * 0.5f;
            for (var i = 1; i < count; i++)
            {
                var a = new Vector2(rect.xMin + rect.width * (i - 1) / (count - 1), mid + Mathf.Clamp(_values[i - 1], -1f, 1f) * half);
                var b = new Vector2(rect.xMin + rect.width * i / (count - 1), mid + Mathf.Clamp(_values[i], -1f, 1f) * half);
                Segment(vh, a, b);
            }
        }

        private void Quad(VertexHelper vh, Vector2 min, Vector2 max)
        {
            var start = vh.currentVertCount;
            vh.AddVert(new Vector3(min.x, min.y), color, Vector2.zero);
            vh.AddVert(new Vector3(min.x, max.y), color, Vector2.zero);
            vh.AddVert(new Vector3(max.x, max.y), color, Vector2.zero);
            vh.AddVert(new Vector3(max.x, min.y), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void Segment(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var direction = (b - a).normalized;
            var side = new Vector2(-direction.y, direction.x) * (lineWidth * 0.5f);
            var start = vh.currentVertCount;
            vh.AddVert(a - side, color, Vector2.zero);
            vh.AddVert(a + side, color, Vector2.zero);
            vh.AddVert(b + side, color, Vector2.zero);
            vh.AddVert(b - side, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
