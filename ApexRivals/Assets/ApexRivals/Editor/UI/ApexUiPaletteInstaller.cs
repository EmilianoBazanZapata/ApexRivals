using System.Text;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ApexRivals.Editor.UI
{
    /// <summary>
    /// Creates the project's <see cref="ApexUiPalette"/> asset (if missing) and assigns it to every UI control with an
    /// empty palette slot in the game scenes and prefabs. The screen builders' styling pass assigns it too, so newly
    /// built controls are connected from the start. Re-running only fills empty slots.
    /// </summary>
    public static class ApexUiPaletteInstaller
    {
        public const string PalettePath = "Assets/ApexRivals/UI/Theme/ApexUiPalette.asset";
        private const string PaletteField = "palette";
        private static readonly string[] Scenes = { "Bootstrap", "MainMenu", "Garage", "Race", "RaceDunes", "RaceSuzuka" };

        [MenuItem("ApexRivals/UI/Assign UI Palette")]
        public static void InstallFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(Install());
            }
        }

        public static ApexUiPalette LoadOrCreate()
        {
            var palette = AssetDatabase.LoadAssetAtPath<ApexUiPalette>(PalettePath);
            if (palette != null) return palette;
            if (!AssetDatabase.IsValidFolder("Assets/ApexRivals/UI/Theme"))
            {
                AssetDatabase.CreateFolder("Assets/ApexRivals/UI", "Theme");
            }

            palette = ScriptableObject.CreateInstance<ApexUiPalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
            AssetDatabase.SaveAssets();
            return palette;
        }

        /// <summary>Fills every empty palette slot under <paramref name="root"/>; returns how many were filled.</summary>
        public static int AssignUnder(GameObject root)
        {
            if (root == null) return 0;
            var palette = LoadOrCreate();
            var assigned = 0;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var serialized = new SerializedObject(behaviour);
                var slot = serialized.FindProperty(PaletteField);
                if (slot == null || slot.propertyType != SerializedPropertyType.ObjectReference || slot.objectReferenceValue != null) continue;
                if (slot.type != "PPtr<$" + nameof(ApexUiPalette) + ">") continue;
                slot.objectReferenceValue = palette;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                assigned++;
            }

            return assigned;
        }

        public static string Install()
        {
            var report = new StringBuilder();
            LoadOrCreate();
            foreach (var name in Scenes)
            {
                var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + name + ".unity", OpenSceneMode.Single);
                var count = 0;
                foreach (var root in scene.GetRootGameObjects()) count += AssignUnder(root);
                if (count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                report.AppendLine(name + ": " + count + " control(s)");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ApexRivals" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var count = AssignUnder(root);
                    if (count == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine(path + ": " + count + " control(s)");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            return report.ToString();
        }
    }
}
