using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Editor.Track
{
    /// <summary>Small shared meshes, baked once in the Editor rather than one GameObject per person.</summary>
    internal sealed class TracksideMeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _motion = new List<Vector2>();
        public int Spectators { get; private set; }

        public Mesh Crowd()
        {
            var random = new System.Random(501);
            var shirts = new Color32[] { new Color32(225, 40, 32, 255), new Color32(230, 227, 211, 255),
                new Color32(36, 111, 172, 255), new Color32(230, 170, 46, 255), new Color32(42, 133, 99, 255) };
            var skins = new Color32[] { new Color32(218, 164, 122, 255), new Color32(145, 91, 62, 255), new Color32(244, 200, 159, 255) };
            // Native Kenney grandStandCovered tread positions, relative to its bottom-centred pivot.
            var heights = new[] { 2.45f, 2.83f, 3.41f, 4f, 4.58f, 5.16f, 5.74f };
            for (var row = 0; row < heights.Length; row++)
                for (var column = 0; column < 10; column++)
                {
                    if (random.NextDouble() < 0.13) continue;
                    var origin = new Vector3(-3.85f + column * 0.85f, heights[row] + 0.03f, -4.17f + row * 0.644f);
                    var phase = (float)random.NextDouble() * Mathf.PI * 2;
                    var shirt = shirts[random.Next(shirts.Length)];
                    var skin = skins[random.Next(skins.Length)];
                    var pants = new Color32(32, 42, 59, 255);
                    Box(origin + new Vector3(0, 0.66f, 0), new Vector3(0.42f, 0.54f, 0.25f), shirt, 0.13f, phase);
                    Box(origin + new Vector3(0, 1.12f, -0.02f), Vector3.one * 0.31f, skin, 0.18f, phase);
                    Box(origin + new Vector3(0, 1.27f, 0), new Vector3(0.33f, 0.1f, 0.31f), random.Next(2) == 0 ? shirt : pants, 0.18f, phase);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Box(origin + new Vector3(side * 0.12f, 0.4f, -0.12f), new Vector3(0.15f, 0.18f, 0.38f), pants, 0, phase);
                        Box(origin + new Vector3(side * 0.12f, 0.2f, -0.28f), new Vector3(0.14f, 0.32f, 0.14f), pants, 0, phase);
                        var shoulder = origin + new Vector3(side * 0.26f, 0.85f, 0);
                        var raised = random.NextDouble() < 0.38;
                        var hand = shoulder + new Vector3(side * 0.22f, raised ? 0.52f : -0.35f, -0.12f);
                        Box((shoulder + hand) * 0.5f, new Vector3(0.14f, Vector3.Distance(shoulder, hand), 0.14f), shirt,
                            raised ? 1 : 0.2f, phase, Quaternion.FromToRotation(Vector3.up, hand - shoulder));
                        Box(hand, Vector3.one * 0.15f, skin, raised ? 1 : 0.2f, phase);
                    }
                    Spectators++;
                }
            return Finish("LowPolyCrowdSection");
        }

        public Mesh Flag()
        {
            const int columns = 8;
            const int rows = 4;
            for (var y = 0; y <= rows; y++)
                for (var x = 0; x <= columns; x++)
                {
                    var u = (float)x / columns;
                    _vertices.Add(new Vector3(u * 2.7f, 13.2f + (float)y / rows * 1.6f, 0));
                    _colors.Add(y == 2 || (x >= 3 && x <= 4) ? new Color32(238, 236, 223, 255) : new Color32(220, 31, 25, 255));
                    _motion.Add(new Vector2(u * u, u * 5));
                    if (x == columns || y == rows) continue;
                    var start = y * (columns + 1) + x;
                    _triangles.AddRange(new[] { start, start + columns + 1, start + 1, start + 1, start + columns + 1, start + columns + 2 });
                }
            return Finish("TracksideFlag");
        }

        public Mesh Pole()
        {
            Box(new Vector3(0, 7.5f, 0), new Vector3(0.14f, 15f, 0.14f), new Color32(160, 168, 174, 255), 0, 0);
            Box(new Vector3(0, 0.18f, 0), new Vector3(0.8f, 0.36f, 0.8f), new Color32(67, 73, 78, 255), 0, 0);
            return Finish("TracksideFlagPole");
        }

        private void Box(Vector3 center, Vector3 size, Color tint, float weight, float phase, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++) corners[i] = center + rotation * Vector3.Scale(size * 0.5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var faces = new[] { 0, 2, 3, 1, 4, 5, 7, 6, 0, 4, 6, 2, 1, 3, 7, 5, 0, 1, 5, 4, 2, 6, 7, 3 };
            for (var face = 0; face < 6; face++)
            {
                var start = _vertices.Count;
                for (var i = 0; i < 4; i++) { _vertices.Add(corners[faces[face * 4 + i]]); _colors.Add(tint); _motion.Add(new Vector2(weight, phase)); }
                _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        private Mesh Finish(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices); mesh.SetColors(_colors); mesh.SetUVs(0, _motion); mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(1.5f); mesh.bounds = bounds; // Include shader motion in culling.
            return mesh;
        }
    }
}
