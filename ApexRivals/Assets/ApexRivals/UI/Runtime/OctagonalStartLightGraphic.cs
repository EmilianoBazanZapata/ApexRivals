using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Flat octagonal lens with a faceted rim, following the low-poly cars.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OctagonalStartLightGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Disc(vh, rect.center, radius, new Color(color.r, color.g, color.b, 0.12f * color.a));
            Disc(vh, rect.center, radius * 0.85f, new Color32(55, 65, 75, 255));
            Disc(vh, rect.center, radius * 0.72f, color);
            Disc(vh, rect.center + Vector2.up * radius * 0.12f, radius * 0.48f,
                new Color(Mathf.Min(1, color.r * 1.15f), Mathf.Min(1, color.g * 1.15f), Mathf.Min(1, color.b * 1.15f), color.a));
        }

        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            var start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            for (var i = 0; i < 8; i++)
            {
                var angle = (22.5f + i * 45f) * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            }
            for (var i = 0; i < 8; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
        }
    }
}
