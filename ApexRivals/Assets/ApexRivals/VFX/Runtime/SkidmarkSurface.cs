using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApexRivals.VFX.Runtime
{
    /// <summary>One bounded world-space mesh for every tire in a race. Old segments fade and ring slots are reused.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SkidmarkSurface : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MarkVertex
        {
            public Vector3 Position;
            public Color32 Color;
            public Vector2 Uv;
            public Vector2 Timing;
        }

        private RaceVfxDefinition _definition;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MarkVertex[] _vertices;
        private MaterialPropertyBlock _properties;
        private int _capacity;
        private int _nextSlot;
        private int _dirtyFrom = int.MaxValue;
        private int _dirtyTo;
        private Bounds _bounds;
        public int TotalSegmentsWritten { get; private set; }
        public int SegmentCount => Mathf.Min(TotalSegmentsWritten, _capacity);
        public Mesh SharedMesh => _mesh;

        public void Initialize(RaceVfxDefinition definition)
        {
            _definition = definition;
            _capacity = Mathf.Clamp(definition.skidMaximumSegments, 128, 8192);
            _vertices = new MarkVertex[_capacity * 4];
            _mesh = new Mesh { name = "RaceTireMarks" };
            _mesh.MarkDynamic();
            _mesh.SetVertexBufferParams(_vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2));
            _mesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length);
            var indices = new int[_capacity * 6];
            for (var i = 0; i < _capacity; i++)
            {
                var vertex = i * 4;
                var triangle = i * 6;
                indices[triangle] = vertex;
                indices[triangle + 1] = vertex + 1;
                indices[triangle + 2] = vertex + 2;
                indices[triangle + 3] = vertex + 2;
                indices[triangle + 4] = vertex + 1;
                indices[triangle + 5] = vertex + 3;
            }
            _mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = definition.skidmarkMaterial;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _properties = new MaterialPropertyBlock();
        }

        public bool TryAddSegment(Vector3 from, Vector3 fromNormal, Vector3 to, Vector3 toNormal, float intensity)
        {
            if (_mesh == null || intensity <= 0f) return false;
            var distance = Vector3.Distance(from, to);
            if (distance < _definition.skidMinimumSpacing || distance > _definition.skidMaximumConnection
                || fromNormal.sqrMagnitude < 0.5f || toNormal.sqrMagnitude < 0.5f
                || Vector3.Dot(fromNormal.normalized, toNormal.normalized) < 0.5f) return false;
            var direction = (to - from).normalized;
            var leftFrom = Vector3.Cross(fromNormal.normalized, direction).normalized * (_definition.skidWidth * 0.5f);
            var leftTo = Vector3.Cross(toNormal.normalized, direction).normalized * (_definition.skidWidth * 0.5f);
            var a = from + fromNormal.normalized * 0.012f;
            var b = to + toNormal.normalized * 0.012f;
            var tint = new Color(1f, 1f, 1f, Mathf.Clamp01(intensity * _definition.skidOpacity));
            var index = _nextSlot * 4;
            Vertex(index, a - leftFrom, new Vector2(0f, 0f), tint);
            Vertex(index + 1, a + leftFrom, new Vector2(1f, 0f), tint);
            Vertex(index + 2, b - leftTo, new Vector2(0f, distance), tint);
            Vertex(index + 3, b + leftTo, new Vector2(1f, distance), tint);
            _dirtyFrom = Mathf.Min(_dirtyFrom, index);
            _dirtyTo = Mathf.Max(_dirtyTo, index + 4);
            if (TotalSegmentsWritten == 0) _bounds = new Bounds(a, Vector3.one);
            _bounds.Encapsulate(a - leftFrom);
            _bounds.Encapsulate(a + leftFrom);
            _bounds.Encapsulate(b - leftTo);
            _bounds.Encapsulate(b + leftTo);
            _nextSlot = (_nextSlot + 1) % _capacity;
            TotalSegmentsWritten++;
            return true;
        }

        private void Vertex(int index, Vector3 position, Vector2 uv, Color tint)
        {
            _vertices[index] = new MarkVertex
            {
                Position = transform.InverseTransformPoint(position), Color = tint, Uv = uv,
                Timing = new Vector2(Time.time, Mathf.Max(1f, _definition.skidLifetime))
            };
        }

        private void LateUpdate()
        {
            if (_mesh == null) return;
            if (_dirtyFrom < _dirtyTo)
            {
                _mesh.SetVertexBufferData(_vertices, _dirtyFrom, _dirtyFrom, _dirtyTo - _dirtyFrom, 0,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                // The scene-owned object has an identity world transform.
                _mesh.bounds = _bounds;
                _dirtyFrom = int.MaxValue;
                _dirtyTo = 0;
            }
            _properties.SetFloat("_MarkTime", Time.time);
            _renderer.SetPropertyBlock(_properties);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
