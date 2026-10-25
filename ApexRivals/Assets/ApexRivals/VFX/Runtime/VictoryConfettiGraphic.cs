using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.VFX.Runtime
{
    /// <summary>Low-poly papers rendered above the results UI, without an extra camera or render texture.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VictoryConfettiGraphic : MaskableGraphic
    {
        private struct Paper
        {
            public float Side, Horizontal, Vertical, Size, Spin, Phase, Birth;
            public Color Color;
        }
        private readonly Paper[] _papers = new Paper[100];
        private float _elapsed = 10f;
        private const float Duration = 4f;
        public bool IsCelebrating => _elapsed < Duration;

        public void Burst()
        {
            var random = new System.Random(731);
            var colors = new[] { new Color(0.95f, 0.12f, 0.12f), new Color(1f, 0.8f, 0.18f),
                new Color(0.15f, 0.65f, 1f), Color.white, new Color(0.35f, 0.9f, 0.6f) };
            for (var i = 0; i < _papers.Length; i++)
                _papers[i] = new Paper
                {
                    Side = i % 2 == 0 ? -1f : 1f,
                    Horizontal = 0.14f + (float)random.NextDouble() * 0.25f,
                    Vertical = 0.7f + (float)random.NextDouble() * 0.5f,
                    Size = 0.006f + (float)random.NextDouble() * 0.005f,
                    Spin = 3f + (float)random.NextDouble() * 7f,
                    Phase = (float)random.NextDouble() * Mathf.PI * 2f,
                    Birth = (float)random.NextDouble() * 0.5f,
                    Color = colors[i % colors.Length]
                };
            _elapsed = 0f;
            raycastTarget = false;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!IsCelebrating) return;
            _elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!IsCelebrating) return;
            var rect = rectTransform.rect;
            var scale = Mathf.Min(rect.width, rect.height);
            for (var i = 0; i < _papers.Length; i++)
            {
                var paper = _papers[i];
                var t = _elapsed - paper.Birth;
                if (t < 0f) continue;
                var x = 0.5f + paper.Side * (0.43f - paper.Horizontal * t);
                var y = -0.05f + paper.Vertical * t - 0.33f * t * t;
                if (y < -0.08f) continue;
                var center = new Vector2(rect.xMin + x * rect.width, rect.yMin + y * rect.height);
                var angle = paper.Phase + t * paper.Spin;
                var right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (paper.Size * scale);
                var up = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * (paper.Size * scale * 0.6f)
                    * (0.25f + 0.75f * Mathf.Abs(Mathf.Cos(t * paper.Spin + paper.Phase)));
                var tint = paper.Color;
                tint.a *= Mathf.Clamp01((Duration - _elapsed) / 0.8f);
                var index = helper.currentVertCount;
                helper.AddVert(center - right - up, tint, Vector2.zero);
                helper.AddVert(center - right + up, tint, Vector2.up);
                helper.AddVert(center + right + up, tint, Vector2.one);
                helper.AddVert(center + right - up, tint, Vector2.right);
                helper.AddTriangle(index, index + 1, index + 2);
                helper.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
