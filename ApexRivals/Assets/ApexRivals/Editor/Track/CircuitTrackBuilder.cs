using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApexRivals.AI.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ApexRivals.Editor.Track
{
    /// <summary>
    /// Creates a circuit only in an empty race scene; existing authored circuits are preserved. Generates from its layout JSON: a closed centreline (metres, x/z, start line first,
    /// clockwise) traced from a reference F1 layout and scaled to ~2 km, with optional per-point heights for bridges.
    /// Each circuit (<see cref="CircuitProfile"/>) has its own scene, layout and folder for the generated meshes. The road, kerbs and run-off are
    /// generated along that spline with Kenney Racing Kit materials; barriers, grandstands, pits, gantry and scenery
    /// are Racing Kit models. AI waypoints and the starting grid are initialized only when empty. Checkpoints remain Editor-owned.
    /// </summary>
    public static class CircuitTrackBuilder
    {
        private const string ModelDirectory = "Assets/ThirdParty/Kenney/RacingKit/Models/";

        /// <summary>The original Race scene circuit.</summary>
        public static readonly CircuitProfile RaceCircuit = new CircuitProfile(
            "Assets/ApexRivals/Scenes/Race.unity",
            "Assets/ApexRivals/Editor/Track/CircuitLayout.json",
            "Assets/ApexRivals/Art/Track/Circuit");

        /// <summary>Dunes circuit, traced from the Zandvoort layout and shortened to ~2.1 km.</summary>
        public static readonly CircuitProfile DunesCircuit = new CircuitProfile(
            "Assets/ApexRivals/Scenes/RaceDunes.unity",
            "Assets/ApexRivals/Editor/Track/DunesLayout.json",
            "Assets/ApexRivals/Art/Track/Dunes");

        /// <summary>Suzuka-style figure of eight shortened to ~2.5 km; the back straight crosses over on a bridge.</summary>
        public static readonly CircuitProfile SuzukaCircuit = new CircuitProfile(
            "Assets/ApexRivals/Scenes/RaceSuzuka.unity",
            "Assets/ApexRivals/Editor/Track/SuzukaLayout.json",
            "Assets/ApexRivals/Art/Track/Suzuka");

        private static string _generatedDirectory = RaceCircuit.GeneratedDirectory;

        private const float SampleSpacing = 1f;
        private const float HalfWidth = 7f;
        private const float RoadHeight = 0.02f;
        private const float LineWidth = 0.3f;
        private const float LineInset = 0.6f;
        private const float KerbWidth = 1.2f;
        private const float KerbHeight = 0.05f;
        private const float KerbStripeLength = 2.5f;
        private const float KerbRadius = 70f;
        private const float RunOffWidth = 6f;
        private const float StraightBarrierOffset = 11f;
        private const float CornerBarrierOffset = 14f;
        private const float BarrierPieceLength = 8f;
        private const float LocalWindow = 60f;
        private const float OtherSectionClearance = HalfWidth + KerbWidth + 2.3f;
        private const float PropScale = 0.8f;
        private const float WaypointSpacing = 12f;
        private const float WaypointHeight = 0.2f;
        private const float SpawnHeight = 0.25f;
        private const float LevelSeparation = 4f;
        private const float ElevatedThreshold = 0.05f;
        private const float DeckHalfWidth = CornerBarrierOffset + 1.5f;
        private const float DeckThickness = 1.2f;
        private const float PillarSpacing = 16f;
        private const float PillarHalfSize = 0.8f;
        private const float PillarLateral = HalfWidth - 1.5f;
        private const float PillarMinimumHeight = 2.5f;
        private const float PillarClearance = CornerBarrierOffset + 1.5f;
        private const int GridSlotCount = 4;
        private const float GridFirstSlotDistance = 10f;
        private const float GridSlotSpacing = 8f;
        private const float GridLateralOffset = 2.8f;

        [MenuItem("ApexRivals/Track/Create Missing Race Circuit")]
        public static void RebuildFromMenu() => RebuildFromMenu(RaceCircuit);

        [MenuItem("ApexRivals/Track/Create Missing Dunes Circuit")]
        public static void RebuildDunesFromMenu() => RebuildFromMenu(DunesCircuit);

        [MenuItem("ApexRivals/Track/Create Missing Suzuka Circuit")]
        public static void RebuildSuzukaFromMenu() => RebuildFromMenu(SuzukaCircuit);

        private static void RebuildFromMenu(CircuitProfile profile)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != profile.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(profile.ScenePath);
            }

            Debug.Log(Rebuild(profile));
        }

        /// <summary>Initializes an empty Race circuit in the open scene; preserves existing content and returns a short summary.</summary>
        public static string Rebuild() => Rebuild(RaceCircuit);

        /// <summary>Initializes an empty circuit in the matching open scene; preserves existing content and returns a short summary.</summary>
        public static string Rebuild(CircuitProfile profile)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != profile.ScenePath) throw new InvalidOperationException("Open the matching circuit scene: " + profile.ScenePath);
            var sceneRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "RaceSceneRoot");
            if (sceneRoot == null) throw new InvalidOperationException("RaceSceneRoot missing in " + scene.path);
            var track = sceneRoot.transform.Find("Track");
            var environment = sceneRoot.transform.Find("Environment");
            var line = sceneRoot.GetComponentInChildren<RacingLine>(true);
            var grid = sceneRoot.GetComponentInChildren<StartingGrid>(true);
            if (track == null || environment == null || line == null || grid == null)
                throw new InvalidOperationException("Missing Track, Environment, RacingLine or StartingGrid in " + scene.path);

            // An existing circuit is authored content. Do not try to reconstruct its missing children:
            // doing so would mix a new layout with edited geometry, checkpoints and spawn positions.
            if (track.Find("Circuit") != null || track.Find("RoadSurface") != null ||
                track.Find("Barriers") != null || environment.Find("CircuitProps") != null ||
                environment.Find("Ground") != null || line.transform.childCount > 0 || grid.transform.childCount > 0 ||
                new SerializedObject(line).FindProperty("waypoints").arraySize > 0 ||
                new SerializedObject(grid).FindProperty("spawnPoints").arraySize > 0 ||
                track.GetComponentsInChildren<RaceCheckpoint>(true).Length > 0)
                return "Circuit preserved: geometry or race layout already exists. Edit it in the Inspector; generation only initializes an empty circuit.";

            _generatedDirectory = profile.GeneratedDirectory;
            var centreline = Centreline.Load(profile.LayoutPath);
            Directory.CreateDirectory(_generatedDirectory);
            var surface = CreateGroup(track, "Circuit");
            BuildSurface(centreline, surface);
            var pillars = BuildBridges(centreline, surface);
            var barriers = CreateGroup(track, "Barriers");
            var barrierCount = BuildBarriers(centreline, barriers);
            var props = CreateGroup(environment, "CircuitProps");
            BuildGround(centreline, environment);
            var propCount = BuildStartArea(centreline, props) + BuildTrackside(centreline, props) + BuildScenery(centreline, props);
            var waypoints = PlaceWaypoints(centreline, line);
            PlaceGrid(centreline, grid);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"Missing circuit created: {centreline.Length:F0} m, {waypoints} waypoints, {barrierCount} barriers, {propCount} props, {pillars} pillars. Configure checkpoints in the Editor.";
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create missing circuit group");
            return root;
        }

        // ---------------------------------------------------------------- surface

        private static void BuildSurface(Centreline c, Transform parent)
        {
            var count = c.Count;
            var road = new MeshBuilder(1);
            for (var i = 0; i <= count; i++)
            {
                var k = i % count;
                var v = c.Distance(i) / (HalfWidth * 2f);
                road.AddPair(c.Offset(k, -HalfWidth, RoadHeight), c.Offset(k, HalfWidth, RoadHeight), new Vector2(0f, v), new Vector2(1f, v), i > 0 ? 0 : -1);
            }

            var roadObject = CreateMeshObject(parent, "Road", road.Build("CircuitRoad"), Material("roadStraight", "road"), true);

            var lines = new MeshBuilder(1);
            foreach (var side in new[] { -1f, 1f })
            {
                var start = lines.VertexCount;
                for (var i = 0; i <= count; i++)
                {
                    var k = i % count;
                    var inner = side * (HalfWidth - LineInset - LineWidth);
                    var outer = side * (HalfWidth - LineInset);
                    lines.AddPair(c.Offset(k, Mathf.Min(inner, outer), RoadHeight + 0.004f), c.Offset(k, Mathf.Max(inner, outer), RoadHeight + 0.004f), Vector2.zero, Vector2.one, i > 0 ? 0 : -1);
                }
            }

            CreateMeshObject(parent, "EdgeLines", lines.Build("CircuitEdgeLines"), Material("roadCornerLargeBorder", "white"), false);

            var kerbs = new MeshBuilder(2);
            var runOff = new MeshBuilder(1);
            foreach (var run in c.CornerRuns(1f / KerbRadius))
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    var first = true;
                    for (var i = run.start; i <= run.end; i++)
                    {
                        var k = c.Wrap(i);
                        if (Mathf.Sign(c.Curvature(k)) == side && Mathf.Abs(c.Curvature(k)) * (HalfWidth + KerbWidth + 2f) > 1f)
                        {
                            // The inside kerb would fold over itself on very tight hairpins: leave a gap there.
                            first = true;
                            continue;
                        }

                        var stripe = ((Mathf.FloorToInt(c.Distance(i) / KerbStripeLength) % 2) + 2) % 2;
                        var a = c.Offset(k, side * HalfWidth, RoadHeight + 0.003f);
                        var b = c.Offset(k, side * (HalfWidth + KerbWidth), RoadHeight + KerbHeight);
                        kerbs.AddPair(side < 0f ? b : a, side < 0f ? a : b, Vector2.zero, Vector2.one, first ? -1 : stripe);
                        first = false;
                    }

                    // Sand run-off only on the outside of the corner.
                    if (Mathf.Sign(c.Curvature(run.peak)) == side)
                    {
                        continue;
                    }

                    first = true;
                    for (var i = run.start; i <= run.end; i++)
                    {
                        var k = c.Wrap(i);
                        var fade = Mathf.Clamp01(Mathf.Min(i - run.start, run.end - i) / 12f);
                        var a = c.Offset(k, side * (HalfWidth + KerbWidth), 0.008f);
                        var b = c.Offset(k, side * (HalfWidth + KerbWidth + RunOffWidth * fade + 0.01f), 0.008f);
                        runOff.AddPair(side < 0f ? b : a, side < 0f ? a : b, Vector2.zero, Vector2.one, first ? -1 : 0);
                        first = false;
                    }
                }
            }

            CreateMeshObject(parent, "Kerbs", kerbs.Build("CircuitKerbs"), new[] { Material("roadCornerLargeBorder", "red"), Material("roadCornerLargeBorder", "white") }, true);
            CreateMeshObject(parent, "RunOff", runOff.Build("CircuitRunOff"), Material("roadCornerLargeSand", "sand"), false);

            // Chequered start/finish line across the road.
            var line = new MeshBuilder(1);
            for (var i = -1; i <= 1; i++)
            {
                var k = c.Wrap(i);
                var v = i + 1;
                line.AddPair(c.Offset(k, -HalfWidth, RoadHeight + 0.006f), c.Offset(k, HalfWidth, RoadHeight + 0.006f), new Vector2(0f, v * 0.5f), new Vector2(HalfWidth, v * 0.5f), i > -1 ? 0 : -1);
            }

            CreateMeshObject(parent, "StartFinishLine", line.Build("CircuitStartLine"), Material("flagCheckers", "checkers"), false);
            roadObject.transform.SetAsFirstSibling();
        }

        private static GameObject CreateMeshObject(Transform parent, string name, Mesh mesh, Material material, bool collider)
        {
            return CreateMeshObject(parent, name, mesh, new[] { material }, collider);
        }

        private static GameObject CreateMeshObject(Transform parent, string name, Mesh mesh, Material[] materials, bool collider)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            if (collider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            MarkStatic(go);
            return go;
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            var path = _generatedDirectory + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                if (File.Exists(path)) throw new InvalidOperationException("Existing mesh cannot be loaded: " + path);
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void BuildGround(Centreline c, Transform environment)
        {
            var bounds = c.Bounds;
            // Reach past the race camera far plane (1000 m) so the ground meets the skybox horizon.
            bounds.Expand(new Vector3(2400f, 0f, 2400f));
            var size = bounds.size;
            var builder = new MeshBuilder(1);
            var tile = 1f / 20f;
            builder.AddPair(new Vector3(bounds.min.x, 0f, bounds.min.z), new Vector3(bounds.max.x, 0f, bounds.min.z), Vector2.zero, new Vector2(size.x * tile, 0f), -1);
            builder.AddPair(new Vector3(bounds.min.x, 0f, bounds.max.z), new Vector3(bounds.max.x, 0f, bounds.max.z), new Vector2(0f, size.z * tile), new Vector2(size.x * tile, size.z * tile), 0);
            var ground = CreateMeshObject(environment, "Ground", builder.Build("CircuitGround"), Material("grass", "grass"), false);
            var box = ground.AddComponent<BoxCollider>();
            box.center = new Vector3(bounds.center.x, -0.5f, bounds.center.z);
            box.size = new Vector3(size.x, 1f, size.z);
            ground.transform.SetSiblingIndex(0);
        }

        // ---------------------------------------------------------------- bridges

        /// <summary>
        /// Gives elevated stretches (a crossover and its ramps) a deck: grass shoulders out to the barriers, side
        /// walls and an underside, plus pillars that keep clear of the road passing underneath.
        /// </summary>
        private static int BuildBridges(Centreline c, Transform parent)
        {
            var first = Enumerable.Range(0, c.Count).FirstOrDefault(i => c.Position(i).y <= ElevatedThreshold);
            if (Enumerable.Range(0, c.Count).All(i => c.Position(i).y <= ElevatedThreshold))
            {
                return 0;
            }

            var deck = new MeshBuilder(2);
            var pillars = new MeshBuilder(1);
            var pillarCount = 0;
            var lastPillar = float.NegativeInfinity;
            for (var n = first; n < first + c.Count; n++)
            {
                if (c.Position(n).y <= ElevatedThreshold)
                {
                    continue;
                }

                var start = n;
                while (n < first + c.Count && c.Position(n).y > ElevatedThreshold)
                {
                    n++;
                }

                // Pad one sample each side so the deck meets the ground at both ramp feet.
                AddDeckRun(c, deck, start - 1, n);
                for (var i = start; i < n; i++)
                {
                    var k = c.Wrap(i);
                    if (c.Position(k).y < PillarMinimumHeight || c.Distance(i) - lastPillar < PillarSpacing)
                    {
                        continue;
                    }

                    foreach (var side in new[] { -1f, 1f })
                    {
                        var foot = c.Offset(k, side * PillarLateral, 0f);
                        foot.y = 0f;
                        if (ClearOfLowerRoad(c, foot))
                        {
                            AddPillar(pillars, foot, c.Tangent(k), c.Position(k).y - DeckThickness);
                            pillarCount++;
                        }
                    }

                    lastPillar = c.Distance(i);
                }
            }

            var concrete = Material("roadStraight", "road");
            CreateMeshObject(parent, "BridgeDeck", deck.Build("CircuitBridgeDeck"), new[] { Material("grass", "grass"), concrete }, true);
            CreateMeshObject(parent, "BridgePillars", pillars.Build("CircuitBridgePillars"), concrete, true);
            return pillarCount;
        }

        /// <summary>Shoulders (grass, facing up), underside and side walls (concrete); the pair order sets each face's side.</summary>
        private static void AddDeckRun(Centreline c, MeshBuilder deck, int from, int to)
        {
            void Strip(Func<int, (Vector3 first, Vector3 second)> pair, int submesh)
            {
                for (var i = from; i <= to; i++)
                {
                    var (a, b) = pair(c.Wrap(i));
                    var v = c.Distance(i) / 8f;
                    deck.AddPair(a, b, new Vector2(0f, v), new Vector2(1f, v), i > from ? submesh : -1);
                }
            }

            Strip(k => (c.Offset(k, -DeckHalfWidth, 0f), c.Offset(k, -HalfWidth, 0f)), 0);
            Strip(k => (c.Offset(k, HalfWidth, 0f), c.Offset(k, DeckHalfWidth, 0f)), 0);
            Strip(k => (c.Offset(k, DeckHalfWidth, -DeckThickness), c.Offset(k, -DeckHalfWidth, -DeckThickness)), 1);
            Strip(k => (c.Offset(k, -DeckHalfWidth, -DeckThickness), c.Offset(k, -DeckHalfWidth, 0f)), 1);
            Strip(k => (c.Offset(k, DeckHalfWidth, 0f), c.Offset(k, DeckHalfWidth, -DeckThickness)), 1);
        }

        private static bool ClearOfLowerRoad(Centreline c, Vector3 foot)
        {
            foreach (var j in c.Near(foot, PillarClearance))
            {
                var other = c.Position(j);
                if (other.y < PillarMinimumHeight && new Vector2(other.x - foot.x, other.z - foot.z).sqrMagnitude < PillarClearance * PillarClearance)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddPillar(MeshBuilder pillars, Vector3 foot, Vector3 tangent, float height)
        {
            var forward = new Vector3(tangent.x, 0f, tangent.z).normalized * PillarHalfSize;
            var right = new Vector3(forward.z, 0f, -forward.x);
            var corners = new[] { foot - forward - right, foot - forward + right, foot + forward + right, foot + forward - right };
            for (var side = 0; side < 4; side++)
            {
                var a = corners[side];
                var b = corners[(side + 1) % 4];
                pillars.AddPair(a, b, Vector2.zero, Vector2.right, -1);
                pillars.AddPair(a + Vector3.up * height, b + Vector3.up * height, Vector2.up, Vector2.one, 0);
            }
        }

        // ---------------------------------------------------------------- barriers

        private static int BuildBarriers(Centreline c, Transform parent)
        {
            var pieces = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                var offsets = new float[c.Count];
                for (var i = 0; i < c.Count; i++)
                {
                    var outside = c.IsCorner(i, 1f / KerbRadius) && Mathf.Sign(c.Curvature(i)) != side;
                    offsets[i] = outside ? CornerBarrierOffset : StraightBarrierOffset;
                }

                offsets = Smooth(offsets, 15);
                Vector3 Point(int i) => c.Offset(c.Wrap(i), side * offsets[c.Wrap(i)], 0f);

                var start = 0;
                while (start < c.Count)
                {
                    var end = start + 1;
                    while (end < start + c.Count && (Point(end) - Point(start)).magnitude < BarrierPieceLength)
                    {
                        end++;
                    }

                    var mid = (start + end) / 2;
                    if (IsClear(c, start, Point(start), offsets[c.Wrap(start)]) && IsClear(c, mid, Point(mid), offsets[c.Wrap(mid)]) && IsClear(c, end, Point(end), offsets[c.Wrap(end)]))
                    {
                        var a = Point(start);
                        var b = Point(end);
                        var corner = c.IsCorner(mid, 1f / KerbRadius);
                        var model = corner ? "barrierWall" : "rail";
                        var piece = Spawn(model, parent, (a + b) * 0.5f, AlongX(b - a, side), Vector3.one * PropScale, true);
                        var stretch = (b - a).magnitude / (piece.GetComponent<BoxCollider>().size.x * PropScale);
                        piece.transform.localScale = new Vector3(PropScale * stretch, PropScale, PropScale);
                        pieces++;
                    }

                    start = end;
                }
            }

            return pieces;
        }

        /// <summary>True when <paramref name="point"/>, offset from sample <paramref name="index"/>, does not fold over its own
        /// corner and stays clear of every other part of the circuit.</summary>
        private static bool IsClear(Centreline c, int index, Vector3 point, float offset, float otherClearance = OtherSectionClearance)
        {
            foreach (var j in c.Near(point, Mathf.Max(offset, otherClearance)))
            {
                if (Mathf.Abs(c.Position(j).y - point.y) > LevelSeparation)
                {
                    // A section on another level (bridge over / road under) never blocks this one.
                    continue;
                }

                var along = c.AlongDistance(c.Wrap(index), j);
                var required = along < LocalWindow ? offset - 0.75f : otherClearance;
                if ((c.Position(j) - point).sqrMagnitude < required * required)
                {
                    return false;
                }
            }

            return true;
        }

        // ---------------------------------------------------------------- props

        private static int BuildStartArea(Centreline c, Transform parent)
        {
            var count = 0;
            var gantryScale = (HalfWidth * 2f + 4f) / 12.6f;
            Spawn("overheadLights", parent, c.Offset(0, 0f, 0f), AlongX(c.Right(0), 1f), Vector3.one * gantryScale, false);
            count++;

            foreach (var side in new[] { -1f, 1f })
            {
                Spawn("flagCheckers", parent, c.Offset(0, side * (StraightBarrierOffset + 1.5f), 0f), Facing(c, 0, side), Vector3.one * PropScale, false);
                count++;
            }

            // Grandstands and fencing along the outside (left) of the main straight, pit garages on the infield side.
            for (var s = -170f; s <= 120f; s += 10f)
            {
                var i = c.IndexAt(s);
                if (TryPlace(c, i, -1f, 24f, 5f, "grandStandCovered", parent, 1f, 180f))
                {
                    count++;
                }

                if (TryPlace(c, i, -1f, 14.5f, 1f, "fenceStraight", parent, PropScale, 0f, alongTrack: true))
                {
                    count++;
                }
            }

            for (var s = -120f; s <= 60f; s += 10f)
            {
                if (TryPlace(c, c.IndexAt(s), 1f, 19f, 5f, s > 40f ? "pitsOffice" : "pitsGarage", parent, 1f, 180f))
                {
                    count++;
                }
            }

            return count;
        }

        private static int BuildTrackside(Centreline c, Transform parent)
        {
            var count = 0;
            var peaks = c.CornerRuns(1f / KerbRadius).Select(r => r.peak).ToList();
            var models = new[] { "bannerTowerRed", "billboard", "bannerTowerGreen", "billboardLow", "camera_exclusive", "lightRedDouble" };
            for (var p = 0; p < peaks.Count; p++)
            {
                var outside = -Mathf.Sign(c.Curvature(peaks[p]));
                var model = models[p % models.Length];
                if (TryPlace(c, peaks[p], outside, CornerBarrierOffset + 9f, 5f, model, parent, PropScale, 180f))
                {
                    count++;
                }
            }

            // Light posts along the straights, just behind the barriers.
            for (var s = 0f; s < c.Length; s += 45f)
            {
                var i = c.IndexAt(s);
                if (c.IsCorner(i, 1f / 150f))
                {
                    continue;
                }

                var side = (Mathf.FloorToInt(s / 45f) % 2 == 0) ? -1f : 1f;
                if (TryPlace(c, i, side, StraightBarrierOffset + 2f, 1f, "lightPostModern", parent, PropScale, 0f))
                {
                    count++;
                }
            }

            return count;
        }

        private static int BuildScenery(Centreline c, Transform parent)
        {
            var random = new System.Random(1971);
            var bounds = c.Bounds;
            bounds.Expand(new Vector3(160f, 0f, 160f));
            var placed = new List<Vector3>();
            var count = 0;

            // Paddock tents and radar on the infield behind the pits.
            foreach (var (s, model) in new[] { (-60f, "tentLong"), (-35f, "tent"), (-10f, "tentClosedLong"), (15f, "radarEquipment") })
            {
                if (TryPlace(c, c.IndexAt(s), 1f, 34f, 7f, model, parent, PropScale, 180f))
                {
                    count++;
                }
            }

            for (var attempt = 0; attempt < 6000 && count < 420; attempt++)
            {
                var point = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble()),
                    0f,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble()));
                if (c.DistanceToCentreline(point) < 26f || placed.Any(p => (p - point).sqrMagnitude < 81f) || IsNearProp(parent, point, 14f))
                {
                    continue;
                }

                var model = random.NextDouble() < 0.6 ? "treeLarge" : "treeSmall";
                var scale = PropScale * Mathf.Lerp(0.75f, 1.1f, (float)random.NextDouble());
                Spawn(model, parent, point, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), Vector3.one * scale, false);
                placed.Add(point);
                count++;
            }

            return count;
        }

        private static bool IsNearProp(Transform parent, Vector3 point, float radius)
        {
            foreach (Transform child in parent)
            {
                if (!child.name.StartsWith("tree", StringComparison.Ordinal) && (child.position - point).sqrMagnitude < radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPlace(Centreline c, int index, float side, float offset, float radius, string model, Transform parent, float scale, float yaw, bool alongTrack = false)
        {
            var point = c.Offset(index, side * offset, 0f);
            if (c.Position(index).y > ElevatedThreshold)
            {
                // Props stand on the ground; beside a bridge or its ramps they would float.
                return false;
            }

            if (!IsClear(c, index, point, offset - radius, StraightBarrierOffset + 1f + radius) || IsNearProp(parent, point, radius * 1.6f))
            {
                return false;
            }

            var rotation = alongTrack ? AlongX(c.Tangent(index), side) : Facing(c, index, side) * Quaternion.Euler(0f, yaw, 0f);
            Spawn(model, parent, point, rotation, Vector3.one * scale, false);
            return true;
        }

        /// <summary>Rotation whose +Z looks from the given side of the track towards the centreline.</summary>
        private static Quaternion Facing(Centreline c, int index, float side)
        {
            return Quaternion.LookRotation(-side * c.Right(index), Vector3.up);
        }

        /// <summary>Rotation whose local +X runs along <paramref name="direction"/> and whose +Z faces away from the road.</summary>
        private static Quaternion AlongX(Vector3 direction, float side)
        {
            var flat = new Vector3(direction.x, 0f, direction.z);
            var rotation = Quaternion.LookRotation(Vector3.Cross(flat.normalized, Vector3.up), Vector3.up);
            rotation = side > 0f ? rotation * Quaternion.Euler(0f, 180f, 0f) : rotation;
            // Tilt pieces that run up or down a ramp; level track is unaffected.
            return Quaternion.FromToRotation(flat.normalized, direction.normalized) * rotation;
        }

        /// <summary>Instantiates a Racing Kit model under a pivot whose origin is the model's bottom centre.</summary>
        private static GameObject Spawn(string model, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, bool collider)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDirectory + model + ".fbx") ?? throw new InvalidOperationException("Missing Racing Kit model " + model);
            var pivot = new GameObject(model);
            pivot.transform.SetParent(parent, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, pivot.transform);
            instance.transform.localPosition = Vector3.zero;

            var bounds = LocalBounds(pivot.transform);
            instance.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            pivot.transform.SetPositionAndRotation(position, rotation);
            pivot.transform.localScale = scale;
            if (collider)
            {
                var box = pivot.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
                box.size = bounds.size;
            }

            MarkStatic(pivot);
            return pivot;
        }

        private static Bounds LocalBounds(Transform pivot)
        {
            var bounds = new Bounds();
            var initialised = false;
            foreach (var filter in pivot.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                var matrix = pivot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (!initialised)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        initialised = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }

        private static void MarkStatic(GameObject root)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        private static Material Material(string model, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelDirectory + model + ".fbx").OfType<Material>().FirstOrDefault(m => m.name == name)
                ?? throw new InvalidOperationException($"Material '{name}' not found in {model}.fbx");
        }

        private static float[] Smooth(float[] values, int radius)
        {
            var result = new float[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var sum = 0f;
                for (var j = -radius; j <= radius; j++)
                {
                    sum += values[(i + j + values.Length) % values.Length];
                }

                result[i] = sum / (radius * 2 + 1);
            }

            return result;
        }

        // ---------------------------------------------------------------- race wiring

        private static int PlaceWaypoints(Centreline c, RacingLine line)
        {
            var existing = new SerializedObject(line).FindProperty("waypoints");
            if (existing.arraySize > 0 || line.transform.childCount > 0) return existing.arraySize;

            var count = Mathf.RoundToInt(c.Length / WaypointSpacing);
            var waypoints = new Transform[count];
            for (var n = 0; n < count; n++)
            {
                var waypoint = new GameObject($"Waypoint_{n:000}").transform;
                waypoint.SetParent(line.transform, false);
                waypoint.position = c.Offset(c.IndexAt(c.Length * n / count), 0f, WaypointHeight);
                waypoints[n] = waypoint;
            }

            var serialized = new SerializedObject(line);
            var property = serialized.FindProperty("waypoints");
            property.arraySize = count;
            for (var n = 0; n < count; n++)
            {
                property.GetArrayElementAtIndex(n).objectReferenceValue = waypoints[n];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return count;
        }

        /// <summary>Grid slots behind the start line, pole first; the roster decides who starts where.</summary>
        private static void PlaceGrid(Centreline c, StartingGrid grid)
        {
            if (grid.transform.childCount > 0 || new SerializedObject(grid).FindProperty("spawnPoints").arraySize > 0) return;

            var slots = new Transform[GridSlotCount];
            for (var n = 0; n < GridSlotCount; n++)
            {
                var i = c.IndexAt(-(GridFirstSlotDistance + GridSlotSpacing * n));
                var lateral = n % 2 == 0 ? GridLateralOffset : -GridLateralOffset;
                var slot = new GameObject($"GridSlot_{n + 1:00}").transform;
                slot.SetParent(grid.transform, false);
                slot.SetPositionAndRotation(c.Offset(i, lateral, SpawnHeight), Quaternion.LookRotation(c.Tangent(i), Vector3.up));
                slots[n] = slot;
            }

            var serialized = new SerializedObject(grid);
            var property = serialized.FindProperty("spawnPoints");
            property.arraySize = GridSlotCount;
            for (var n = 0; n < GridSlotCount; n++)
            {
                property.GetArrayElementAtIndex(n).objectReferenceValue = slots[n];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Painted grid boxes in front of each slot.
            var parent = grid.transform.root.Find("Track/Circuit");
            var marks = new MeshBuilder(1);
            foreach (var slot in slots)
            {
                var forward = slot.forward;
                var right = slot.right;
                var front = slot.position + forward * 2.6f;
                front.y = RoadHeight + 0.006f;
                var back = front - forward * 0.35f;
                marks.AddPair(back - right * 1.4f, back + right * 1.4f, Vector2.zero, Vector2.one, -1);
                marks.AddPair(front - right * 1.4f, front + right * 1.4f, Vector2.zero, Vector2.one, 0);
            }

            CreateMeshObject(parent, "GridMarks", marks.Build("CircuitGridMarks"), Material("roadCornerLargeBorder", "white"), false);
        }

        // ---------------------------------------------------------------- helpers

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _triangles;

            public MeshBuilder(int submeshes)
            {
                _triangles = Enumerable.Range(0, submeshes).Select(_ => new List<int>()).ToArray();
            }

            public int VertexCount => _vertices.Count;

            /// <summary>Adds a left/right vertex pair; a submesh &gt;= 0 joins it to the previous pair with a quad.</summary>
            public void AddPair(Vector3 left, Vector3 right, Vector2 leftUv, Vector2 rightUv, int submesh)
            {
                _vertices.Add(left);
                _vertices.Add(right);
                _uvs.Add(leftUv);
                _uvs.Add(rightUv);
                if (submesh < 0)
                {
                    return;
                }

                var b = _vertices.Count - 2;
                var a = b - 2;
                _triangles[submesh].AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = _triangles.Length;
                for (var s = 0; s < _triangles.Length; s++)
                {
                    mesh.SetTriangles(_triangles[s], s);
                }

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return SaveMesh(mesh);
            }
        }

        /// <summary>A circuit's race scene, centreline layout and the folder its generated meshes are saved to.</summary>
        public sealed class CircuitProfile
        {
            public CircuitProfile(string scenePath, string layoutPath, string generatedDirectory)
            {
                ScenePath = scenePath;
                LayoutPath = layoutPath;
                GeneratedDirectory = generatedDirectory;
            }

            public string ScenePath { get; }
            public string LayoutPath { get; }
            public string GeneratedDirectory { get; }
        }

        /// <summary>Closed centreline resampled every <see cref="SampleSpacing"/> metres.</summary>
        private sealed class Centreline
        {
            private readonly Vector3[] _positions;
            private readonly Vector3[] _tangents;
            private readonly float[] _curvature;
            private readonly Dictionary<Vector2Int, List<int>> _cells = new Dictionary<Vector2Int, List<int>>();
            private const float CellSize = 10f;

            private Centreline(List<Vector3> positions)
            {
                _positions = positions.ToArray();
                var n = _positions.Length;
                _tangents = new Vector3[n];
                var curvature = new float[n];
                for (var i = 0; i < n; i++)
                {
                    _tangents[i] = (_positions[(i + 1) % n] - _positions[(i - 1 + n) % n]).normalized;
                }

                for (var i = 0; i < n; i++)
                {
                    var a = _tangents[(i - 2 + n) % n];
                    var b = _tangents[(i + 2) % n];
                    curvature[i] = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad / (4f * SampleSpacing);
                }

                _curvature = Smooth(curvature, 4);
                Length = n * SampleSpacing;
                var bounds = new Bounds(_positions[0], Vector3.zero);
                for (var i = 0; i < n; i++)
                {
                    bounds.Encapsulate(_positions[i]);
                    var key = Cell(_positions[i]);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        _cells[key] = list = new List<int>();
                    }

                    list.Add(i);
                }

                Bounds = bounds;
            }

            public int Count => _positions.Length;
            public float Length { get; }
            public Bounds Bounds { get; }

            public static Centreline Load(string path)
            {
                var json = File.ReadAllText(path);
                var points = ParsePoints(json);
                var heights = ParseArray(json, "heights");
                for (var i = 0; i < points.Count && i < heights.Length; i++)
                {
                    points[i] = new Vector3(points[i].x, heights[i], points[i].z);
                }

                return new Centreline(Resample(Sample(points), SampleSpacing));
            }

            public int Wrap(int i) => ((i % Count) + Count) % Count;
            public float Distance(int i) => i * SampleSpacing;
            public int IndexAt(float distance) => Wrap(Mathf.RoundToInt(distance / SampleSpacing));
            public Vector3 Position(int i) => _positions[Wrap(i)];
            public Vector3 Tangent(int i) => _tangents[Wrap(i)];
            public Vector3 Right(int i) => new Vector3(_tangents[Wrap(i)].z, 0f, -_tangents[Wrap(i)].x);

            /// <summary>Signed curvature (1/m); positive turns right (clockwise seen from above).</summary>
            public float Curvature(int i) => _curvature[Wrap(i)];
            public bool IsCorner(int i, float threshold) => Mathf.Abs(Curvature(i)) > threshold;
            public Vector3 Offset(int i, float lateral, float height) => Position(i) + Right(i) * lateral + Vector3.up * height;

            public float AlongDistance(int a, int b)
            {
                var d = Mathf.Abs(a - b);
                return Mathf.Min(d, Count - d) * SampleSpacing;
            }

            public IEnumerable<int> Near(Vector3 point, float radius)
            {
                var min = Cell(point - new Vector3(radius, 0f, radius));
                var max = Cell(point + new Vector3(radius, 0f, radius));
                for (var x = min.x; x <= max.x; x++)
                {
                    for (var y = min.y; y <= max.y; y++)
                    {
                        if (_cells.TryGetValue(new Vector2Int(x, y), out var list))
                        {
                            foreach (var i in list)
                            {
                                yield return i;
                            }
                        }
                    }
                }
            }

            public float DistanceToCentreline(Vector3 point)
            {
                var best = float.MaxValue;
                foreach (var i in Near(point, 40f))
                {
                    best = Mathf.Min(best, (Position(i) - point).sqrMagnitude);
                }

                return Mathf.Sqrt(best);
            }

            /// <summary>Corner runs (start/end sample indices, possibly past Count) padded so kerbs lead in and out.</summary>
            public List<(int start, int end, int peak)> CornerRuns(float threshold)
            {
                var runs = new List<(int start, int end, int peak)>();
                var first = 0;
                while (first < Count && IsCorner(first, threshold))
                {
                    first++;
                }

                for (var i = first; i < first + Count; i++)
                {
                    if (!IsCorner(i, threshold))
                    {
                        continue;
                    }

                    var start = i;
                    var peak = i;
                    while (i < first + Count && IsCorner(i, threshold) && Mathf.Sign(Curvature(i)) == Mathf.Sign(Curvature(start)))
                    {
                        if (Mathf.Abs(Curvature(i)) > Mathf.Abs(Curvature(peak)))
                        {
                            peak = i;
                        }

                        i++;
                    }

                    if (i - start >= 4)
                    {
                        runs.Add((start - 6, i + 6, Wrap(peak)));
                    }
                }

                return runs;
            }

            private static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));

            private static List<Vector3> ParsePoints(string json)
            {
                // {"points": [[x, z], ...], "heights": [y, ...]} - JsonUtility cannot read jagged arrays, so parse the numbers directly.
                var numbers = ParseArray(json, "points");
                var points = new List<Vector3>();
                for (var i = 0; i + 1 < numbers.Length; i += 2)
                {
                    points.Add(new Vector3(numbers[i], 0f, numbers[i + 1]));
                }

                return points;
            }

            /// <summary>Every number inside the (possibly nested) array stored under <paramref name="key"/>; empty when absent.</summary>
            private static float[] ParseArray(string json, string key)
            {
                var keyIndex = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
                if (keyIndex < 0)
                {
                    return Array.Empty<float>();
                }

                var open = json.IndexOf('[', keyIndex);
                var close = open;
                for (var depth = 0; close < json.Length; close++)
                {
                    depth += json[close] == '[' ? 1 : json[close] == ']' ? -1 : 0;
                    if (depth == 0)
                    {
                        break;
                    }
                }

                return json.Substring(open, close - open + 1)
                    .Split(new[] { '[', ']', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            }

            /// <summary>Closed centripetal Catmull-Rom through the layout points.</summary>
            private static List<Vector3> Sample(List<Vector3> p)
            {
                var result = new List<Vector3>();
                var n = p.Count;
                for (var i = 0; i < n; i++)
                {
                    var p0 = p[(i - 1 + n) % n];
                    var p1 = p[i];
                    var p2 = p[(i + 1) % n];
                    var p3 = p[(i + 2) % n];
                    var steps = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / 0.25f));
                    for (var s = 0; s < steps; s++)
                    {
                        result.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
                    }
                }

                return result;
            }

            private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
            {
                float Knot(float ti, Vector3 a, Vector3 b) => ti + Mathf.Sqrt(Mathf.Max((b - a).magnitude, 1e-4f));
                var t0 = 0f;
                var t1 = Knot(t0, p0, p1);
                var t2 = Knot(t1, p1, p2);
                var t3 = Knot(t2, p2, p3);
                var u = Mathf.Lerp(t1, t2, t);
                var a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
                var a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
                var a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;
                var b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
                var b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;
                return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
            }

            private static List<Vector3> Resample(List<Vector3> dense, float spacing)
            {
                var cumulative = new float[dense.Count + 1];
                for (var i = 0; i < dense.Count; i++)
                {
                    cumulative[i + 1] = cumulative[i] + (dense[(i + 1) % dense.Count] - dense[i]).magnitude;
                }

                var total = cumulative[dense.Count];
                var count = Mathf.RoundToInt(total / spacing);
                var step = total / count;
                var result = new List<Vector3>(count);
                var j = 0;
                for (var k = 0; k < count; k++)
                {
                    var d = k * step;
                    while (cumulative[j + 1] < d)
                    {
                        j++;
                    }

                    var t = (d - cumulative[j]) / Mathf.Max(cumulative[j + 1] - cumulative[j], 1e-6f);
                    result.Add(Vector3.Lerp(dense[j], dense[(j + 1) % dense.Count], t));
                }

                return result;
            }
        }
    }
}
