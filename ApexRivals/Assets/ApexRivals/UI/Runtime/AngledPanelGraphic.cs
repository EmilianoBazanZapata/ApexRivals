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

            AddVertex(vertexHelper, new Vector2(left + leftBottomInset, bottom));
            AddVertex(vertexHelper, new Vector2(left + leftTopInset, top));
            AddVertex(vertexHelper, new Vector2(right - rightTopInset, top));
            AddVertex(vertexHelper, new Vector2(right - rightBottomInset, bottom));

            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
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
