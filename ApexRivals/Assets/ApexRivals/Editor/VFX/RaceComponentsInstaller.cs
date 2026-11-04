using System.Text;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VFX.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApexRivals.Editor.VFX
{
    /// <summary>
    /// Puts on the car prefabs and in the race scenes the race components that used to be added in code when a race
    /// started:
    /// <list type="bullet">
    /// <item>Every car prefab: PlayerVehicleNitro (disabled; the race enables it for the player only),
    /// PlayerCheckpointRecovery, VehicleRaceVfx and VehicleSkidmarks.</item>
    /// <item>Every race scene: the RaceTireMarks surface (renderer, material, no shadows or probes), inactive until
    /// a race installs it, assigned to the RaceVfxSceneInstaller.</item>
    /// </list>
    /// Re-running only adds what is missing.
    /// </summary>
    public static class RaceComponentsInstaller
    {
        private const string PrefabFolder = "Assets/ApexRivals/Vehicle/Prefabs";
        private const string VfxDefinitionPath = "Assets/ApexRivals/Content/Configuration/VFX/RaceVfxDefinition.asset";
        private static readonly string[] RaceScenes = { "Race", "RaceDunes", "RaceSuzuka", "RaceRiverdale" };

        [MenuItem("ApexRivals/Vehicle/Install Race Components On Cars")]
        public static void InstallFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(Install());
            }
        }

        public static string Install()
        {
            var report = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var car = root.GetComponent<RaceVehicleComposition>();
                    if (car == null) continue;
                    var added = new StringBuilder();
                    if (root.GetComponent<PlayerVehicleNitro>() == null)
                    {
                        root.AddComponent<PlayerVehicleNitro>().enabled = false;
                        added.Append(" nitro");
                    }

                    added.Append(Ensure<PlayerCheckpointRecovery>(root, " recovery"));
                    added.Append(Ensure<VehicleRaceVfx>(root, " vfx"));
                    added.Append(Ensure<VehicleSkidmarks>(root, " skidmarks"));
                    if (added.Length == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine(System.IO.Path.GetFileNameWithoutExtension(path) + ":" + added);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var definition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(VfxDefinitionPath);
            foreach (var name in RaceScenes)
            {
                var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + name + ".unity", OpenSceneMode.Single);
                report.AppendLine(name + ": " + InstallTireMarks(scene, definition));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            return report.ToString();
        }

        private static string Ensure<T>(GameObject root, string label) where T : Component
        {
            if (root.GetComponent<T>() != null) return string.Empty;
            root.AddComponent<T>();
            return label;
        }

        private static string InstallTireMarks(UnityEngine.SceneManagement.Scene scene, RaceVfxDefinition definition)
        {
            var installer = Object.FindFirstObjectByType<RaceVfxSceneInstaller>(FindObjectsInactive.Include);
            if (installer == null) return "RaceVfxSceneInstaller not found";
            var refs = new SerializedObject(installer);
            var slot = refs.FindProperty("tireMarks");
            if (slot.objectReferenceValue != null) return "tire marks already set up";

            // A scene root at the world origin: the marks stay on the road, never move with anything.
            var marks = new GameObject("RaceTireMarks", typeof(MeshFilter), typeof(MeshRenderer), typeof(SkidmarkSurface));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marks, scene);
            var renderer = marks.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = definition != null ? definition.skidmarkMaterial : null;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            marks.SetActive(false);
            slot.objectReferenceValue = marks.GetComponent<SkidmarkSurface>();
            refs.ApplyModifiedPropertiesWithoutUndo();
            return "tire marks placed";
        }
    }
}
