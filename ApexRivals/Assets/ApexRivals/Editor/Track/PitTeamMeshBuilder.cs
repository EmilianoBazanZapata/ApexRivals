using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Editor.Track
{
    /// <summary>Shared low-poly pit crew and equipment, using the trackside vertex-colour shader.</summary>
    public static class PitTeamMeshBuilder
    {
        public static Mesh Crew(Color uniform)
        {
            var geometry = new Geometry();
            var dark = new Color32(30, 36, 44, 255);
            var skin = new[] { new Color32(218, 164, 122, 255), new Color32(145, 91, 62, 255), new Color32(244, 200, 159, 255) };
            for (var person = 0; person < 3; person++)
            {
                var origin = new Vector3(-2.1f + person * 2.1f, 0, person == 1 ? -0.6f : 0);
                var phase = person * 2.1f;
                geometry.Box(origin + new Vector3(0, 1.05f, 0), new Vector3(.48f, .65f, .3f), uniform, .08f, phase);
                geometry.Box(origin + new Vector3(0, 1.57f, 0), Vector3.one * .32f, skin[person], .15f, phase);
                geometry.Box(origin + new Vector3(0, 1.77f, 0), new Vector3(.37f, .1f, .36f), uniform, .15f, phase);
                geometry.Box(origin + new Vector3(0, 1.68f, 0), new Vector3(.39f, .07f, .12f), dark, .15f, phase);
                foreach (var side in new[] { -1f, 1f })
                {
                    geometry.Box(origin + new Vector3(side * .13f, .41f, 0), new Vector3(.19f, .78f, .22f), dark);
                    geometry.Box(origin + new Vector3(side * .13f, .07f, -.09f), new Vector3(.23f, .14f, .38f), dark);
                    geometry.Box(origin + new Vector3(side * .22f, 1.57f, 0), new Vector3(.12f, .22f, .18f), dark, .15f, phase);
                    var shoulder = origin + new Vector3(side * .3f, 1.28f, 0);
                    var hand = origin + new Vector3(side * .4f, person == 1 ? 1.12f : .9f, -.38f);
                    geometry.Box((shoulder + hand) * .5f, new Vector3(.15f, Vector3.Distance(shoulder, hand), .15f), uniform,
                        .3f, phase, Quaternion.FromToRotation(Vector3.up, hand - shoulder));
                    geometry.Box(hand, Vector3.one * .16f, dark, .3f, phase);
                }
                // A tablet for the engineer, impact wrenches for the mechanics.
                geometry.Box(origin + new Vector3(0, person == 1 ? 1.12f : .92f, -.42f),
                    person == 1 ? new Vector3(.5f, .33f, .06f) : new Vector3(.55f, .12f, .12f), dark, .3f, phase);
            }
            return geometry.Finish("PitCrew_ThreeTechnicians");
        }

        public static Mesh Equipment()
        {
            var g = new Geometry();
            var steel = new Color32(125, 139, 150, 255);
            var rubber = new Color32(28, 31, 35, 255);
            var red = new Color32(184, 40, 35, 255);
            // Tool trolley with drawers, handle and four castors.
            g.Box(new Vector3(-3.5f, .65f, -.2f), new Vector3(1.1f, .95f, .65f), red);
            g.Box(new Vector3(-3.5f, 1.17f, -.2f), new Vector3(1.2f, .1f, .75f), steel);
            for (var i = 0; i < 3; i++) g.Box(new Vector3(-3.5f, .4f + i * .25f, -.54f), new Vector3(.8f, .035f, .045f), steel);
            foreach (var x in new[] { -3.9f, -3.1f })
                foreach (var z in new[] { -.42f, .02f }) g.Box(new Vector3(x, .13f, z), Vector3.one * .22f, rubber);
            // Compressor with motor and carrying frame.
            g.Box(new Vector3(3.4f, .45f, -.2f), new Vector3(1.1f, .55f, .65f), steel);
            g.Box(new Vector3(3.4f, .9f, -.2f), new Vector3(.6f, .35f, .45f), rubber);
            foreach (var x in new[] { 2.95f, 3.85f }) g.Box(new Vector3(x, .15f, -.2f), new Vector3(.18f, .3f, .75f), rubber);
            // Low floor jack, safely parked beside the crew.
            g.Box(new Vector3(2.7f, .14f, -1.7f), new Vector3(.5f, .22f, 1.15f), red);
            g.Box(new Vector3(2.7f, .7f, -2.13f), new Vector3(.07f, 1.2f, .07f), steel);
            return g.Finish("PitEquipment_TrolleyCompressorJack");
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> motion = new List<Vector2>();

            public void Box(Vector3 center, Vector3 size, Color color, float weight = 0, float phase = 0, Quaternion rotation = default)
            {
                if (rotation == default) rotation = Quaternion.identity;
                var corners = new Vector3[8];
                for (var i = 0; i < 8; i++) corners[i] = center + rotation * Vector3.Scale(size * .5f,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var faces = new[] { 0, 2, 3, 1, 4, 5, 7, 6, 0, 4, 6, 2, 1, 3, 7, 5, 0, 1, 5, 4, 2, 6, 7, 3 };
                for (var face = 0; face < 6; face++)
                {
                    var start = vertices.Count;
                    for (var i = 0; i < 4; i++) { vertices.Add(corners[faces[face * 4 + i]]); colors.Add(color); motion.Add(new Vector2(weight, phase)); }
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, motion); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var bounds = mesh.bounds; bounds.Expand(.3f); mesh.bounds = bounds;
                return mesh;
            }
        }
    }
}
