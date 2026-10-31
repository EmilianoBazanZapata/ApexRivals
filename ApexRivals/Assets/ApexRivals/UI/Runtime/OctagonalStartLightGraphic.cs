using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Flat octagonal lens with a faceted rim, following the low-poly cars.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OctagonalStartLightGraphic : MaskableGraphic
    {
        [SerializeField, Tooltip("Metal rim between the halo and the lens.")] private Color rimColor = new Color32(55, 65, 75, 255);
        [SerializeField, Range(0f, 1f), Tooltip("Opacity of the soft halo around the lamp, relative to the lamp color.")] private float haloOpacity = 0.12f;
        [SerializeField, Range(1f, 2f), Tooltip("How much brighter the highlight on the lens is than the lamp color.")] private float highlightBoost = 1.15f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Disc(vh, rect.center, radius, new Color(color.r, color.g, color.b, haloOpacity * color.a));
            Disc(vh, rect.center, radius * 0.85f, rimColor);
            Disc(vh, rect.center, radius * 0.72f, color);
            Disc(vh, rect.center + Vector2.up * radius * 0.12f, radius * 0.48f,
                new Color(Mathf.Min(1, color.r * highlightBoost), Mathf.Min(1, color.g * highlightBoost), Mathf.Min(1, color.b * highlightBoost), color.a));
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
