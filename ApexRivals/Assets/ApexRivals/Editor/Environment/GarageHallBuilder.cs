using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ApexRivals.Editor.Environment
{
    /// <summary>
    /// Builds the garage hall shared by the main menu and the Garage screen with ProBuilder shapes: a workshop around
    /// the display car's turntable with a mezzanine office and stairs on the left, shelving and an Apex Rivals banner
    /// on the back wall, a closed roll-up door on the right, a skylight and hanging fluorescent
    /// lights. Each scene lays it out in a frame centred on its display car and turned to face its camera. Creates
    /// only a missing "GarageInterior"; existing content is preserved.
    /// </summary>
    public static class GarageHallBuilder
    {
        private const string MaterialFolder = "Assets/ApexRivals/Art/Materials/MainMenuGarage";
        private const string RootName = "GarageInterior";

        /// <summary>The main menu: the hero car sits on the turntable behind the menu buttons.</summary>
        public static readonly HallProfile MainMenu = new HallProfile(
            "Assets/ApexRivals/Scenes/MainMenu.unity", "MainMenuSceneRoot", "Environment",
            "Environment/VehicleDisplay/HeroVehicle", "Cameras/MainMenuCamera", 0.15f);

        /// <summary>The Garage screen: the previewed car spawns on VehicleDisplayAnchor.</summary>
        public static readonly HallProfile Garage = new HallProfile(
            "Assets/ApexRivals/Scenes/Garage.unity", "GarageSceneRoot", "Environment",
            "Environment/VehicleDisplayAnchor", "Environment/GarageCamera", 0.28f);

        private const float HalfWidth = 16f;
        private const float HalfDepth = 13f;
        private const float Height = 10f;
        private const float Wall = 0.4f;
        private const float MezzanineHeight = 3.7f;
        private const float DoorLeft = 5f;
        private const float DoorRight = 12f;
        private const float DoorHeight = 6f;

        private static Palette _m;
        private static float _turntableTop;

        [MenuItem("ApexRivals/Environment/Create Missing Main Menu Garage")]
        public static void BuildMainMenuFromMenu() => BuildFromMenu(MainMenu);

        [MenuItem("ApexRivals/Environment/Create Missing Garage Screen Hall")]
        public static void BuildGarageFromMenu() => BuildFromMenu(Garage);

        private static void BuildFromMenu(HallProfile profile)
        {
            if (EditorSceneManager.GetActiveScene().path != profile.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(profile.ScenePath);
            }

            Debug.Log(Build(profile));
        }

        /// <summary>Creates the hall in the profile's open scene when it is missing; returns a short summary.</summary>
        public static string Build(HallProfile profile)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != profile.ScenePath) throw new InvalidOperationException("Open " + profile.ScenePath + " first.");
            var sceneRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == profile.SceneRoot);
            var environment = sceneRoot != null ? sceneRoot.transform.Find(profile.EnvironmentPath) : null;
            var display = sceneRoot != null ? sceneRoot.transform.Find(profile.DisplayPath) : null;
            var camera = sceneRoot != null ? sceneRoot.transform.Find(profile.CameraPath) : null;
            if (environment == null || display == null || camera == null)
                throw new InvalidOperationException($"{profile.SceneRoot} needs {profile.EnvironmentPath}, {profile.DisplayPath} and {profile.CameraPath}.");
            if (environment.Find(RootName) != null) return "Garage hall preserved: " + RootName + " already exists.";

            _m = new Palette();
            _turntableTop = profile.TurntableTop;
            var font = sceneRoot.GetComponentsInChildren<Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);

            // Frame: origin under the display car, +Z away from the camera. A spawn anchor has no renderers yet.
            var renderers = display.GetComponentsInChildren<Renderer>();
            var center = renderers.Length > 0 ? renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }).center : display.position;
            var view = camera.forward;
            view.y = 0f;
            var root = new GameObject(RootName).transform;
            root.SetParent(environment, false);
            root.SetPositionAndRotation(new Vector3(center.x, 0f, center.z), Quaternion.LookRotation(view.normalized, Vector3.up));
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create garage hall");

            // The sky shows through the door and the skylight.
            var cameraComponent = camera.GetComponent<UnityEngine.Camera>();
            if (cameraComponent != null) cameraComponent.clearFlags = CameraClearFlags.Skybox;

            BuildShell(Group(root, "Shell"));
            BuildTurntable(Group(root, "Turntable"));
            BuildMezzanine(Group(root, "Mezzanine"));
            BuildBackWall(Group(root, "BackWallDressing"), font);
            BuildDoorAndExterior(Group(root, "RollUpDoor"));
            BuildCeiling(Group(root, "Ceiling"));
            BuildProps(Group(root, "Props"));
            var lights = BuildLighting(Group(root, "Lighting"));

            EditorSceneManager.MarkSceneDirty(scene);
            return $"Garage hall created: {root.GetComponentsInChildren<ProBuilderMesh>().Length} ProBuilder meshes, {lights} lights.";
        }

        // ---------------------------------------------------------------- architecture

        private static void BuildShell(Transform parent)
        {
            Box(parent, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(HalfWidth * 2f, 0.2f, HalfDepth * 2f), _m.Floor, true);

            // Back wall with the roll-up door opening on the right.
            var backZ = HalfDepth + Wall * 0.5f;
            var leftWidth = DoorLeft + HalfWidth;
            Box(parent, "BackWall_Left", new Vector3(-HalfWidth + leftWidth * 0.5f, Height * 0.5f, backZ), new Vector3(leftWidth, Height, Wall), _m.WallBlock);
            Box(parent, "BackWall_Right", new Vector3((DoorRight + HalfWidth) * 0.5f, Height * 0.5f, backZ), new Vector3(HalfWidth - DoorRight, Height, Wall), _m.WallBlock);
            Box(parent, "BackWall_Header", new Vector3((DoorLeft + DoorRight) * 0.5f, (DoorHeight + Height) * 0.5f, backZ), new Vector3(DoorRight - DoorLeft, Height - DoorHeight, Wall), _m.WallBlock);
            Box(parent, "SideWall_Left", new Vector3(-HalfWidth - Wall * 0.5f, Height * 0.5f, 0f), new Vector3(Wall, Height, HalfDepth * 2f + Wall * 2f), _m.WallBlock);
            Box(parent, "SideWall_Right", new Vector3(HalfWidth + Wall * 0.5f, Height * 0.5f, 0f), new Vector3(Wall, Height, HalfDepth * 2f + Wall * 2f), _m.WallBlock);
            Box(parent, "FrontWall", new Vector3(0f, Height * 0.5f, -HalfDepth - Wall * 0.5f), new Vector3(HalfWidth * 2f, Height, Wall), _m.WallBlock);

            // Darker block wainscot and the red brand stripe around the hall (broken by the door).
            const float wainscot = 2.2f;
            foreach (var (name, from, to) in new[] { ("Left", -HalfWidth, DoorLeft - 0.6f), ("Right", DoorRight + 0.6f, HalfWidth) })
            {
                Box(parent, "Wainscot_Back" + name, new Vector3((from + to) * 0.5f, wainscot * 0.5f, HalfDepth - 0.05f), new Vector3(to - from, wainscot, 0.1f), _m.WallLower);
                Box(parent, "Stripe_Back" + name, new Vector3((from + to) * 0.5f, 2.75f, HalfDepth - 0.06f), new Vector3(to - from, 0.45f, 0.1f), _m.Red);
            }

            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (HalfWidth - 0.05f);
                var label = side < 0f ? "Left" : "Right";
                Box(parent, "Wainscot_" + label, new Vector3(x, wainscot * 0.5f, 0f), new Vector3(0.1f, wainscot, HalfDepth * 2f), _m.WallLower);
                Box(parent, "Stripe_" + label, new Vector3(x, 2.75f, 0f), new Vector3(0.1f, 0.45f, HalfDepth * 2f), _m.Red);
            }

            // Steel columns along the side walls.
            for (var z = -HalfDepth + 2f; z < HalfDepth; z += 6f)
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    Box(parent, "Column", new Vector3(side * (HalfWidth - 0.35f), Height * 0.5f, z), new Vector3(0.6f, Height, 0.6f), _m.Steel);
                }
            }

            // Painted floor markings: a bay line along the back, a lane to the door and parking diagonals.
            Box(parent, "FloorLine_Back", new Vector3(5.5f, 0.005f, 9f), new Vector3(21f, 0.01f, 0.14f), _m.Yellow);
            Box(parent, "FloorLine_Lane", new Vector3(DoorLeft - 0.4f, 0.005f, -2f), new Vector3(0.14f, 0.01f, 22f), _m.Yellow);
            for (var i = 0; i < 3; i++)
            {
                var line = Box(parent, "FloorLine_Bay", new Vector3(10.5f + i * 1.6f, 0.005f, -6.5f), new Vector3(0.14f, 0.01f, 6f), _m.Yellow);
                line.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }
        }

        private static void BuildTurntable(Transform parent)
        {
            var disc = Cylinder(parent, "TurntableDisc", new Vector3(0f, 0f, 0f), 3.6f, _turntableTop, 8, _m.Concrete);
            disc.transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f);
            disc.gameObject.AddComponent<BoxCollider>();
            var trim = Pipe(parent, "TurntableTrim", new Vector3(0f, 0f, 0f), 3.95f, 0.35f, 0.05f, 8, _m.Steel);
            trim.transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f);
            var ring = Pipe(parent, "TurntableRing", new Vector3(0f, 0f, 0f), 4.45f, 0.22f, 0.012f, 8, _m.Yellow);
            ring.transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f);
        }

        private static void BuildMezzanine(Transform parent)
        {
            const float front = 5.5f;
            const float edge = -6f;
            var slabDepth = HalfDepth - front;
            Box(parent, "Slab", new Vector3((edge - HalfWidth) * 0.5f, MezzanineHeight - 0.15f, front + slabDepth * 0.5f), new Vector3(HalfWidth + edge, 0.3f, slabDepth), _m.Steel, true);
            Box(parent, "SlabFascia", new Vector3((edge - HalfWidth) * 0.5f, MezzanineHeight - 0.45f, front + 0.05f), new Vector3(HalfWidth + edge, 0.35f, 0.12f), _m.Red);
            foreach (var p in new[] { new Vector2(edge + 0.2f, front + 0.2f), new Vector2(edge + 0.2f, 9.5f), new Vector2(-11f, front + 0.2f) })
            {
                Box(parent, "Post", new Vector3(p.x, (MezzanineHeight - 0.3f) * 0.5f, p.y), new Vector3(0.3f, MezzanineHeight - 0.3f, 0.3f), _m.Steel);
            }

            // Office box on the deck with a lit window band facing the hall.
            const float officeFront = 9.2f;
            const float officeRight = -8f;
            var officeTop = MezzanineHeight + 3f;
            var officeWidth = officeRight + HalfWidth;
            var cx = (officeRight - HalfWidth) * 0.5f;
            Box(parent, "Office_Sill", new Vector3(cx, MezzanineHeight + 0.45f, officeFront), new Vector3(officeWidth, 0.9f, 0.25f), _m.Navy);
            Box(parent, "Office_Lintel", new Vector3(cx, officeTop - 0.3f, officeFront), new Vector3(officeWidth, 0.6f, 0.25f), _m.Navy);
            Box(parent, "Office_Window", new Vector3(cx, MezzanineHeight + 1.95f, officeFront + 0.05f), new Vector3(officeWidth - 0.2f, 2.1f, 0.05f), _m.WindowGlow);
            for (var x = -HalfWidth + 0.1f; x <= officeRight; x += 2.6f)
            {
                Box(parent, "Office_Mullion", new Vector3(x, MezzanineHeight + 1.95f, officeFront - 0.05f), new Vector3(0.14f, 2.1f, 0.14f), _m.Steel);
            }

            Box(parent, "Office_Side", new Vector3(officeRight, (MezzanineHeight + officeTop) * 0.5f, (officeFront + HalfDepth) * 0.5f), new Vector3(0.25f, officeTop - MezzanineHeight, HalfDepth - officeFront), _m.Navy);
            Box(parent, "Office_Roof", new Vector3(cx, officeTop + 0.1f, (officeFront + HalfDepth) * 0.5f), new Vector3(officeWidth + 0.3f, 0.2f, HalfDepth - officeFront + 0.3f), _m.Steel);
            Cylinder(parent, "Office_Plant", new Vector3(-13.5f, MezzanineHeight + 0.9f, officeFront + 0.7f), 0.25f, 0.6f, 8, _m.Plant);

            // Yellow safety rail along the deck edge and the side facing the hall.
            Rail(parent, new Vector3(-13.6f, MezzanineHeight, front + 0.1f), new Vector3(edge, MezzanineHeight, front + 0.1f));
            Rail(parent, new Vector3(edge + 0.05f, MezzanineHeight, front + 0.1f), new Vector3(edge + 0.05f, MezzanineHeight, officeFront - 0.2f));

            // Stairs along the left wall, climbing towards the back onto the deck.
            const float stairWidth = 1.6f;
            const float stairRun = 6.5f;
            var stairX = -HalfWidth + 0.3f + stairWidth * 0.5f;
            var stair = ShapeGenerator.GenerateStair(PivotLocation.Center, new Vector3(stairWidth, MezzanineHeight, stairRun), 16, true);
            Finish(stair, parent, "Stairs", new Vector3(stairX, MezzanineHeight * 0.5f, front - stairRun * 0.5f), _m.Steel);
            var bottom = new Vector3(stairX + stairWidth * 0.5f, 0f, front - stairRun);
            var top = new Vector3(stairX + stairWidth * 0.5f, MezzanineHeight, front);
            Rail(parent, bottom, top);
        }

        private static void BuildBackWall(Transform parent, Font font)
        {
            var z = HalfDepth - 0.1f;

            // Workshop under the mezzanine: tool board, bench, tool chest and a stool.
            Box(parent, "ToolBoard", new Vector3(-11f, 2.1f, z - 0.05f), new Vector3(5f, 1.5f, 0.08f), _m.Cardboard);
            for (var i = 0; i < 14; i++)
            {
                var x = -13.2f + i * 0.32f;
                var tall = 0.3f + (i * 7 % 5) * 0.08f;
                Box(parent, "Tool", new Vector3(x, 2.2f, z - 0.12f), new Vector3(0.08f, tall, 0.04f), _m.Steel);
            }

            Box(parent, "Workbench_Top", new Vector3(-10.5f, 0.95f, z - 0.55f), new Vector3(4.6f, 0.1f, 0.9f), _m.Wood);
            Box(parent, "Workbench_Frame", new Vector3(-10.5f, 0.45f, z - 0.55f), new Vector3(4.4f, 0.9f, 0.8f), _m.Steel);
            Box(parent, "ToolChest", new Vector3(-13.6f, 0.55f, z - 0.55f), new Vector3(1.4f, 1.1f, 0.8f), _m.Red);
            for (var i = 0; i < 4; i++)
            {
                Box(parent, "ToolChest_Drawer", new Vector3(-13.6f, 0.2f + i * 0.26f, z - 0.96f), new Vector3(1.2f, 0.03f, 0.02f), _m.Steel);
            }

            Cylinder(parent, "Stool", new Vector3(-8.6f, 0f, z - 1.4f), 0.22f, 0.7f, 10, _m.Red);
            Box(parent, "Monitor", new Vector3(-11.8f, 1.25f, z - 0.45f), new Vector3(0.6f, 0.45f, 0.05f), _m.Navy);
            Box(parent, "UnderDeckLight", new Vector3(-11f, MezzanineHeight - 0.35f, z - 1.5f), new Vector3(3f, 0.06f, 0.25f), _m.LightTube);

            // Shelving with boxes and paint cans next to the deck.
            foreach (var shelfX in new[] { -3.8f, -0.4f })
            {
                Shelf(parent, new Vector3(shelfX, 0f, z - 0.55f));
            }

            // Barrels and tyre stacks against the wall.
            for (var i = 0; i < 3; i++)
            {
                Barrel(parent, new Vector3(2.4f + i * 0.85f, 0f, z - 0.6f));
            }

            TireStack(parent, new Vector3(1.6f, 0f, z - 1.6f), 4);
            TireStack(parent, new Vector3(-6.3f, 0f, z - 0.8f), 3);

            // Brand banner above the shelves.
            var banner = Box(parent, "Banner_ApexRivals", new Vector3(-1.8f, 5.6f, z - 0.05f), new Vector3(6.4f, 2.4f, 0.06f), _m.Banner);
            if (font != null)
            {
                BannerText(banner.transform, font);
            }

            // Red checkered flag hanging right of the door.
            var flagZ = z - 0.05f;
            Box(parent, "Flag", new Vector3(14f, 5f, flagZ), new Vector3(2f, 3.4f, 0.04f), _m.Red);
            const float cell = 0.42f;
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    if ((row + column) % 2 == 1) continue;
                    Box(parent, "FlagSquare", new Vector3(14f + (column - 1.5f) * cell, 5.6f - (row - 1.5f) * cell, flagZ - 0.03f), new Vector3(cell, cell, 0.02f), _m.White);
                }
            }
        }

        private static void BuildDoorAndExterior(Transform parent)
        {
            var z = HalfDepth;
            var centerX = (DoorLeft + DoorRight) * 0.5f;
            var width = DoorRight - DoorLeft;

            // Closed roll-up shutter filling the opening: ribbed slats, a row of vision slots and the drum housing above.
            Box(parent, "Shutter", new Vector3(centerX, DoorHeight * 0.5f, z - 0.25f), new Vector3(width, DoorHeight, 0.12f), _m.Shutter);
            for (var y = 0.45f; y < DoorHeight - 0.1f; y += 0.45f)
            {
                Box(parent, "ShutterSlat", new Vector3(centerX, y, z - 0.32f), new Vector3(width - 0.1f, 0.05f, 0.03f), _m.Steel);
            }

            for (var i = 0; i < 3; i++)
            {
                Box(parent, "ShutterSlot", new Vector3(DoorLeft + 1.6f + i * 1.9f, DoorHeight - 1.6f, z - 0.33f), new Vector3(1.1f, 0.25f, 0.02f), _m.WindowGlow);
            }

            Box(parent, "ShutterBottomRail", new Vector3(centerX, 0.08f, z - 0.33f), new Vector3(width, 0.16f, 0.06f), _m.Yellow);

            Cylinder(parent, "ShutterDrum", new Vector3((DoorLeft + DoorRight) * 0.5f, DoorHeight + 0.05f, z - 0.5f), 0.45f, DoorRight - DoorLeft + 0.6f, 12, _m.Steel)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Hazard-striped jambs either side of the opening.
            foreach (var x in new[] { DoorLeft - 0.3f, DoorRight + 0.3f })
            {
                Box(parent, "Jamb", new Vector3(x, DoorHeight * 0.5f, z - 0.2f), new Vector3(0.6f, DoorHeight, 0.6f), _m.WallLower);
                for (var band = 0; band < 6; band++)
                {
                    Box(parent, "HazardBand", new Vector3(x, 0.2f + band * 0.4f, z - 0.52f), new Vector3(0.64f, 0.4f, 0.05f), band % 2 == 0 ? _m.Yellow : _m.Black);
                }
            }

        }

        private static void BuildCeiling(Transform parent)
        {
            // Roof slab around a skylight that lets the low sun in.
            const float skyLeft = -2f, skyRight = 10f, skyBack = 10f, skyFront = -2f;
            var y = Height + 0.15f;
            Box(parent, "Roof_Left", new Vector3((-HalfWidth + skyLeft) * 0.5f, y, 0f), new Vector3(skyLeft + HalfWidth, 0.3f, HalfDepth * 2f), _m.Ceiling);
            Box(parent, "Roof_Right", new Vector3((skyRight + HalfWidth) * 0.5f, y, 0f), new Vector3(HalfWidth - skyRight, 0.3f, HalfDepth * 2f), _m.Ceiling);
            Box(parent, "Roof_Back", new Vector3((skyLeft + skyRight) * 0.5f, y, (skyBack + HalfDepth) * 0.5f), new Vector3(skyRight - skyLeft, 0.3f, HalfDepth - skyBack), _m.Ceiling);
            Box(parent, "Roof_Front", new Vector3((skyLeft + skyRight) * 0.5f, y, (skyFront - HalfDepth) * 0.5f), new Vector3(skyRight - skyLeft, 0.3f, skyFront + HalfDepth), _m.Ceiling);
            for (var x = skyLeft; x <= skyRight + 0.01f; x += 2f)
            {
                Box(parent, "SkylightBar", new Vector3(x, y - 0.1f, (skyBack + skyFront) * 0.5f), new Vector3(0.12f, 0.35f, skyBack - skyFront), _m.Steel);
            }

            for (var z = skyFront; z <= skyBack + 0.01f; z += 3f)
            {
                Box(parent, "SkylightBar", new Vector3((skyLeft + skyRight) * 0.5f, y - 0.1f, z), new Vector3(skyRight - skyLeft, 0.35f, 0.12f), _m.Steel);
            }

            // Steel trusses across the hall.
            for (var z = -HalfDepth + 1f; z < HalfDepth; z += 4f)
            {
                Box(parent, "Truss", new Vector3(0f, Height - 0.8f, z), new Vector3(HalfWidth * 2f, 0.55f, 0.35f), _m.Steel);
            }

            Box(parent, "Girder", new Vector3(-4f, Height - 1.3f, 0f), new Vector3(0.45f, 0.45f, HalfDepth * 2f), _m.Steel);
            Box(parent, "Girder", new Vector3(12f, Height - 1.3f, 0f), new Vector3(0.45f, 0.45f, HalfDepth * 2f), _m.Steel);
        }

        private static void BuildProps(Transform parent)
        {
            // Foreground crates on the left, cone, barrel and tyre rack on the right.
            Box(parent, "Crate", new Vector3(-9f, 0.6f, -6.5f), new Vector3(1.6f, 1.2f, 1.2f), _m.Wood).transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Box(parent, "Crate", new Vector3(-10.4f, 0.45f, -4.6f), new Vector3(1.2f, 0.9f, 1.2f), _m.Wood).transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
            Box(parent, "Crate", new Vector3(-8.8f, 1.55f, -6.4f), new Vector3(1f, 0.7f, 0.9f), _m.Wood).transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Barrel(parent, new Vector3(9.8f, 0f, -6.2f));
            Cone(parent, new Vector3(8.6f, 0f, -7.2f));
            Cone(parent, new Vector3(7.4f, 0f, -5.4f));

            // Red tyre rack along the right wall.
            const float rackX = 14.9f;
            foreach (var z in new[] { 2f, 6f })
            {
                Box(parent, "RackPost", new Vector3(rackX, 1.4f, z), new Vector3(0.12f, 2.8f, 0.12f), _m.Red);
                Box(parent, "RackPost", new Vector3(rackX - 1.1f, 1.4f, z), new Vector3(0.12f, 2.8f, 0.12f), _m.Red);
            }

            foreach (var y in new[] { 0.35f, 1.55f, 2.75f })
            {
                Box(parent, "RackBar", new Vector3(rackX - 0.55f, y, 4f), new Vector3(1.2f, 0.08f, 4.2f), _m.Red);
            }

            for (var level = 0; level < 2; level++)
            {
                for (var i = 0; i < 6; i++)
                {
                    var tire = Cylinder(parent, "Tire", new Vector3(rackX - 0.55f, 0.82f + level * 1.2f, 2.35f + i * 0.66f), 0.42f, 0.28f, 14, _m.Tire);
                    tire.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    tire.transform.localPosition += new Vector3(0f, 0f, -0.14f);
                }
            }

            TireStack(parent, new Vector3(12.6f, 0f, 9.6f), 5);
            TireStack(parent, new Vector3(13.5f, 0f, 10.4f), 3);
        }

        private static int BuildLighting(Transform parent)
        {
            var count = 0;

            // Low warm sun through the skylight and the door.
            var sun = new GameObject("SunsetSun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.74f, 0.5f);
            sun.intensity = 3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.38f, -0.62f, -0.68f), Vector3.up);
            RenderSettings.sun = sun;
            count++;

            // Hanging fluorescent fittings, each with a soft fill below it.
            foreach (var p in new[] { new Vector3(-8f, 7f, 1.5f), new Vector3(0f, 7.2f, 6f), new Vector3(7f, 7f, 1f), new Vector3(-1f, 7.4f, -5f), new Vector3(9f, 7.3f, 9f) })
            {
                Box(parent, "Fixture", p, new Vector3(2.2f, 0.14f, 0.38f), _m.Steel);
                Box(parent, "FixtureTube", p + Vector3.down * 0.09f, new Vector3(2.05f, 0.04f, 0.26f), _m.LightTube);
                foreach (var side in new[] { -0.9f, 0.9f })
                {
                    Box(parent, "FixtureCable", new Vector3(p.x + side, (p.y + Height - 0.8f) * 0.5f, p.z), new Vector3(0.025f, Height - 0.8f - p.y, 0.025f), _m.Black);
                }

                var fill = new GameObject("FixtureLight").AddComponent<Light>();
                fill.transform.SetParent(parent, false);
                fill.transform.localPosition = p + Vector3.down * 0.4f;
                fill.type = LightType.Point;
                fill.color = new Color(1f, 0.93f, 0.82f);
                fill.range = 14f;
                fill.intensity = 22f;
                fill.shadows = LightShadows.None;
                count++;
            }

            var office = new GameObject("OfficeLight").AddComponent<Light>();
            office.transform.SetParent(parent, false);
            office.transform.localPosition = new Vector3(-12f, MezzanineHeight + 2.4f, 11.2f);
            office.type = LightType.Point;
            office.color = new Color(1f, 0.85f, 0.6f);
            office.range = 7f;
            office.intensity = 10f;
            count++;

            RenderSettings.skybox = _m.Sky;
            // Explicit warm ambient: it does not depend on baking the environment lighting from the sky.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.56f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.41f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.21f, 0.19f);
            DynamicGI.UpdateEnvironment();
            return count;
        }

        // ---------------------------------------------------------------- props

        private static void Shelf(Transform parent, Vector3 origin)
        {
            const float width = 3f, depth = 0.8f, height = 2.6f;
            foreach (var x in new[] { -width * 0.5f, width * 0.5f })
            {
                foreach (var z in new[] { -depth * 0.5f, depth * 0.5f })
                {
                    Box(parent, "ShelfPost", origin + new Vector3(x, height * 0.5f, z), new Vector3(0.07f, height, 0.07f), _m.Steel);
                }
            }

            var random = new System.Random(Mathf.RoundToInt(origin.x * 100f));
            for (var level = 0; level < 4; level++)
            {
                var y = 0.15f + level * 0.8f;
                Box(parent, "ShelfBoard", origin + new Vector3(0f, y, 0f), new Vector3(width, 0.05f, depth), _m.Steel);
                if (level == 3) continue;
                for (var x = -width * 0.5f + 0.4f; x < width * 0.5f - 0.3f; x += 0.75f)
                {
                    if (random.NextDouble() < 0.2) continue;
                    if (random.NextDouble() < 0.3)
                    {
                        Cylinder(parent, "PaintCan", origin + new Vector3(x, y + 0.03f, 0f), 0.13f, 0.32f, 10, random.NextDouble() < 0.5 ? _m.Red : _m.White);
                        continue;
                    }

                    var size = new Vector3(0.6f, 0.35f + (float)random.NextDouble() * 0.3f, 0.55f);
                    Box(parent, "Box", origin + new Vector3(x, y + 0.03f + size.y * 0.5f, 0f), size, _m.Cardboard);
                }
            }
        }

        private static void Barrel(Transform parent, Vector3 position)
        {
            Cylinder(parent, "Barrel", position, 0.38f, 1.1f, 14, _m.Red);
            Cylinder(parent, "BarrelBand", position + Vector3.up * 0.42f, 0.385f, 0.22f, 14, _m.White);
        }

        private static void TireStack(Transform parent, Vector3 position, int count)
        {
            for (var i = 0; i < count; i++)
            {
                Cylinder(parent, "Tire", position + new Vector3(i % 2 * 0.04f, i * 0.3f, 0f), 0.42f, 0.29f, 14, _m.Tire);
            }
        }

        private static void Cone(Transform parent, Vector3 position)
        {
            Box(parent, "ConeBase", position + Vector3.up * 0.03f, new Vector3(0.55f, 0.06f, 0.55f), _m.Orange);
            var cone = ShapeGenerator.GenerateCone(PivotLocation.FirstVertex, 0.22f, 0.75f, 12);
            Finish(cone, parent, "Cone", Vector3.zero, _m.Orange);
            var bounds = cone.GetComponent<MeshRenderer>().localBounds;
            cone.transform.localPosition = position + new Vector3(-bounds.center.x, 0.06f - bounds.min.y, -bounds.center.z);
            Box(parent, "ConeStripe", position + Vector3.up * 0.42f, new Vector3(0.24f, 0.1f, 0.24f), _m.White);
        }

        private static void Rail(Transform parent, Vector3 from, Vector3 to)
        {
            var run = to - from;
            var length = run.magnitude;
            var rotation = Quaternion.LookRotation(run.normalized, Vector3.up);
            foreach (var h in new[] { 1.05f, 0.55f })
            {
                var bar = Box(parent, "Rail", (from + to) * 0.5f + Vector3.up * h, new Vector3(0.07f, 0.07f, length), _m.Yellow);
                bar.transform.localRotation = rotation;
            }

            var posts = Mathf.Max(2, Mathf.CeilToInt(length / 1.4f) + 1);
            for (var i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(from, to, i / (float)(posts - 1));
                Box(parent, "RailPost", p + Vector3.up * 0.525f, new Vector3(0.07f, 1.05f, 0.07f), _m.Yellow);
            }
        }

        private static void BannerText(Transform banner, Font font)
        {
            var canvasObject = new GameObject("BannerCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(banner, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = new Vector2(640f, 240f);
            rect.localScale = Vector3.one * 0.01f;
            rect.localPosition = new Vector3(0f, 0f, -0.04f);
            Label(rect, font, "APEX", new Color(0.95f, 0.95f, 0.93f), new Vector2(0f, 30f), 120);
            Label(rect, font, "RIVALS", new Color(0.9f, 0.09f, 0.07f), new Vector2(60f, -62f), 70);
        }

        private static void Label(RectTransform parent, Font font, string text, Color color, Vector2 position, int size)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(600f, 140f);
            rect.anchoredPosition = position;
            var label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyle.BoldAndItalic;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
        }

        // ---------------------------------------------------------------- ProBuilder helpers

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static ProBuilderMesh Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider = false)
        {
            var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            Finish(mesh, parent, name, center, material);
            if (collider) mesh.gameObject.AddComponent<BoxCollider>();
            return mesh;
        }

        /// <summary>Upright cylinder standing on <paramref name="basePosition"/>.</summary>
        private static ProBuilderMesh Cylinder(Transform parent, string name, Vector3 basePosition, float radius, float height, int sides, Material material)
        {
            var mesh = ShapeGenerator.GenerateCylinder(PivotLocation.Center, sides, radius, height, 0);
            Finish(mesh, parent, name, basePosition + Vector3.up * (height * 0.5f), material);
            return mesh;
        }

        private static ProBuilderMesh Pipe(Transform parent, string name, Vector3 basePosition, float radius, float thickness, float height, int sides, Material material)
        {
            var mesh = ShapeGenerator.GeneratePipe(PivotLocation.Center, radius, height, thickness, sides, 0);
            Finish(mesh, parent, name, basePosition + Vector3.up * (height * 0.5f + 0.002f), material);
            return mesh;
        }

        private static void Finish(ProBuilderMesh mesh, Transform parent, string name, Vector3 localPosition, Material material)
        {
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = localPosition;
            mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();
            GameObjectUtility.SetStaticEditorFlags(mesh.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
        }

        // ---------------------------------------------------------------- materials

        private sealed class Palette
        {
            public readonly Material Floor = Lit("Garage_Floor", new Color(0.46f, 0.43f, 0.4f), 0.42f);
            public readonly Material Concrete = Lit("Garage_Concrete", new Color(0.55f, 0.52f, 0.49f), 0.3f);
            public readonly Material WallBlock = Lit("Garage_WallBlock", new Color(0.5f, 0.46f, 0.44f), 0.15f);
            public readonly Material WallLower = Lit("Garage_WallLower", new Color(0.3f, 0.29f, 0.3f), 0.15f);
            public readonly Material Ceiling = Lit("Garage_Ceiling", new Color(0.24f, 0.22f, 0.22f), 0.1f);
            public readonly Material Steel = Lit("Garage_Steel", new Color(0.2f, 0.2f, 0.22f), 0.45f, 0.6f);
            public readonly Material Red = Lit("Garage_Red", new Color(0.72f, 0.08f, 0.07f), 0.4f);
            public readonly Material White = Lit("Garage_White", new Color(0.9f, 0.89f, 0.86f), 0.3f);
            public readonly Material Black = Lit("Garage_Black", new Color(0.05f, 0.05f, 0.06f), 0.3f);
            public readonly Material Yellow = Lit("Garage_Yellow", new Color(0.95f, 0.72f, 0.08f), 0.35f);
            public readonly Material Orange = Lit("Garage_Orange", new Color(1f, 0.38f, 0.08f), 0.35f);
            public readonly Material Navy = Lit("Garage_Navy", new Color(0.1f, 0.15f, 0.22f), 0.3f);
            public readonly Material Cardboard = Lit("Garage_Cardboard", new Color(0.78f, 0.6f, 0.3f), 0.15f);
            public readonly Material Wood = Lit("Garage_Wood", new Color(0.45f, 0.38f, 0.32f), 0.15f);
            public readonly Material Tire = Lit("Garage_Tire", new Color(0.07f, 0.07f, 0.08f), 0.25f);
            public readonly Material Shutter = Lit("Garage_Shutter", new Color(0.36f, 0.34f, 0.34f), 0.3f, 0.3f);
            public readonly Material Banner = Lit("Garage_Banner", new Color(0.07f, 0.07f, 0.09f), 0.2f);
            public readonly Material Plant = Lit("Garage_Plant", new Color(0.2f, 0.45f, 0.2f), 0.2f);
            public readonly Material WindowGlow = Lit("Garage_WindowGlow", new Color(0.32f, 0.24f, 0.16f), 0.6f, 0f, new Color(0.85f, 0.55f, 0.28f) * 0.55f);
            public readonly Material LightTube = Lit("Garage_LightTube", Color.white, 0.5f, 0f, new Color(1f, 0.96f, 0.88f) * 3f);
            public readonly Material Sky = SunsetSky();

            private static Material Lit(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
            {
                var path = MaterialFolder + "/" + name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) return existing;
                if (File.Exists(path)) throw new InvalidOperationException("Existing material cannot be loaded: " + path);
                Directory.CreateDirectory(MaterialFolder);
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader missing.");
                var material = new Material(shader) { name = name };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                if (emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            private static Material SunsetSky()
            {
                var path = MaterialFolder + "/Garage_SunsetSky.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) return existing;
                if (File.Exists(path)) throw new InvalidOperationException("Existing material cannot be loaded: " + path);
                Directory.CreateDirectory(MaterialFolder);
                var shader = Shader.Find("Skybox/Procedural") ?? throw new InvalidOperationException("Procedural skybox shader missing.");
                var material = new Material(shader) { name = "Garage_SunsetSky" };
                material.SetFloat("_SunSize", 0.05f);
                material.SetFloat("_AtmosphereThickness", 1.6f);
                material.SetColor("_SkyTint", new Color(0.6f, 0.55f, 0.65f));
                material.SetColor("_GroundColor", new Color(0.35f, 0.3f, 0.3f));
                material.SetFloat("_Exposure", 1.15f);
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
        }

        /// <summary>A scene that hosts the hall: where its display car stands and which camera frames it.</summary>
        public sealed class HallProfile
        {
            public HallProfile(string scenePath, string sceneRoot, string environmentPath, string displayPath, string cameraPath, float turntableTop)
            {
                ScenePath = scenePath;
                SceneRoot = sceneRoot;
                EnvironmentPath = environmentPath;
                DisplayPath = displayPath;
                CameraPath = cameraPath;
                TurntableTop = turntableTop;
            }

            public string ScenePath { get; }
            public string SceneRoot { get; }
            public string EnvironmentPath { get; }
            public string DisplayPath { get; }
            public string CameraPath { get; }

            /// <summary>Height the display car's wheels rest at.</summary>
            public float TurntableTop { get; }
        }
    }
}
