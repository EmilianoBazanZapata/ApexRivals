using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Progress drawn as a row of "&gt;" chevrons ending in a chequered flag: completed chevrons are red with a soft
    /// glow, the leading one is outlined in white, the rest are grey. <see cref="Value"/> is the 0..1 completion.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChevronProgressBar : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float value;
        [SerializeField, Min(1)] private int segments = 12;
        [SerializeField, Min(0f)] private float spacing = 6f;
        [SerializeField, Range(0.1f, 0.8f), Tooltip("Chevron tip depth as a fraction of its width.")] private float depth = 0.42f;
        [SerializeField, Range(0.1f, 0.9f), Tooltip("Arm thickness as a fraction of its width.")] private float thickness = 0.55f;
        [SerializeField] private bool showFlag = true;
        [SerializeField] private Color litColor = ApexUiVisualStyle.PrimaryRed;
        [SerializeField] private Color unlitColor = new Color32(0x4E, 0x53, 0x5B, 0xFF);
        [SerializeField] private Color leadOutlineColor = Color.white;
        [SerializeField] private Color glowColor = new Color(0.9f, 0.09f, 0.07f, 0.28f);
        [SerializeField, Tooltip("Pole and light squares of the chequered flag.")] private Color flagLightColor = Color.white;
        [SerializeField, Tooltip("Dark squares of the chequered flag.")] private Color flagDarkColor = new Color(0.08f, 0.08f, 0.09f, 1f);

        public float Value
        {
            get => value;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, this.value))
                {
                    return;
                }

                this.value = clamped;
                SetVerticesDirty();
            }
        }

        /// <summary>Lit chevrons for the current value; any progress lights at least one.</summary>
        public int LitSegments => value <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(value * segments), 1, segments);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var flagWidth = showFlag ? rect.height * 1.25f : 0f;
            var flagGap = showFlag ? rect.height * 0.35f : 0f;
            var barWidth = rect.width - flagWidth - flagGap;
            var cellWidth = (barWidth - spacing * (segments - 1)) / segments;
            if (cellWidth <= 0f)
            {
                return;
            }

            var lit = LitSegments;
            var glow = rect.height * 0.12f;
            var outline = Mathf.Max(1.5f, rect.height * 0.06f);
            for (var index = 0; index < segments; index++)
            {
                var x = rect.xMin + index * (cellWidth + spacing);
                var isLit = index < lit;
                if (isLit)
                {
                    Chevron(vh, x - glow, rect.yMin - glow, cellWidth + 2f * glow, rect.height + 2f * glow, glowColor);
                }

                if (isLit && index == lit - 1 && lit < segments)
                {
                    Chevron(vh, x - outline, rect.yMin - outline, cellWidth + 2f * outline, rect.height + 2f * outline, leadOutlineColor);
                }

                Chevron(vh, x, rect.yMin, cellWidth, rect.height, isLit ? litColor : unlitColor);
            }

            if (showFlag)
            {
                var dividerX = rect.xMin + barWidth + flagGap * 0.5f;
                Quad(vh, dividerX - 1f, rect.yMin, dividerX + 1f, rect.yMax, unlitColor);
                Flag(vh, rect.xMax - flagWidth, rect.yMin, flagWidth, rect.height);
            }
        }

        /// <summary>"&gt;" in the cell (x, y, w, h): two parallelograms meeting at the tip.</summary>
        private void Chevron(VertexHelper vh, float x, float y, float w, float h, Color tint)
        {
            var d = w * depth;
            var t = w * thickness;
            var mid = y + h * 0.5f;
            var top = y + h;
            // Upper arm: top-left, top-right, tip, inner notch. Lower arm mirrors it.
            Polygon(vh, tint, new Vector2(x, top), new Vector2(x + t, top), new Vector2(x + t + d, mid), new Vector2(x + d, mid));
            Polygon(vh, tint, new Vector2(x + d, mid), new Vector2(x + t + d, mid), new Vector2(x + t, y), new Vector2(x, y));
        }

        /// <summary>Pole plus a 4x3 chequered cloth in the top part of the area.</summary>
        private void Flag(VertexHelper vh, float x, float y, float w, float h)
        {
            var poleWidth = Mathf.Max(2f, w * 0.07f);
            var poleX = x + w * 0.12f;
            Quad(vh, poleX, y, poleX + poleWidth, y + h, flagLightColor);

            const int columns = 4;
            const int rows = 3;
            var clothLeft = poleX + poleWidth;
            var clothWidth = x + w - clothLeft;
            var clothHeight = h * 0.62f;
            var clothTop = y + h;
            var cellW = clothWidth / columns;
            var cellH = clothHeight / rows;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var tint = (row + column) % 2 == 0 ? flagLightColor : flagDarkColor;
                    var left = clothLeft + column * cellW;
                    var topY = clothTop - row * cellH;
                    Quad(vh, left, topY - cellH, left + cellW, topY, tint);
                }
            }
        }

        private void Quad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color tint)
        {
            Polygon(vh, tint, new Vector2(xMin, yMax), new Vector2(xMax, yMax), new Vector2(xMax, yMin), new Vector2(xMin, yMin));
        }

        private void Polygon(VertexHelper vh, Color tint, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var multiplied = (Color32)(tint * color);
            var start = vh.currentVertCount;
            vh.AddVert(a, multiplied, Vector2.zero);
            vh.AddVert(b, multiplied, Vector2.zero);
            vh.AddVert(c, multiplied, Vector2.zero);
            vh.AddVert(d, multiplied, Vector2.zero);
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
