using System;
using System.IO;
using System.Linq;
using ApexRivals.AI.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.DrivingAssistance.Runtime;
using ApexRivals.Race.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ApexRivals.Editor.Track
{
    public static class DrivingLineBuilder
    {
        private const string Folder = "Assets/ApexRivals/DrivingAssistance";
        [MenuItem("Apex Rivals/Assistance/Create and Bind Driving Line")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before rebuilding the driving line.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder + "/Art");
                AssetDatabase.Refresh();
                var shader = Shader.Find("ApexRivals/Assistance/Driving Line");

                var materialPath = Folder + "/Art/DrivingLine.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    if (File.Exists(materialPath)) throw new InvalidOperationException("Existing material cannot be loaded: " + materialPath);
                    if (shader == null) throw new InvalidOperationException("Driving line shader is missing.");
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var prefabPath = Folder + "/DrivingLine.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    if (File.Exists(prefabPath)) throw new InvalidOperationException("Existing prefab cannot be loaded: " + prefabPath);
                    var root = new GameObject("DrivingLine");
                    try
                    {
                        var line = root.AddComponent<DynamicDrivingLine>();
                        root.GetComponent<MeshRenderer>().enabled = false;
                        var serialized = new SerializedObject(line);
                        serialized.FindProperty("lineMaterial").objectReferenceValue = material;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                foreach (var name in new[] { "Race", "RaceDunes", "RaceSuzuka", "RaceRiverdale" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + name + ".unity");
                    var line = Find<DynamicDrivingLine>(scene);
                    var created = line == null;
                    if (created)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        if (asset == null || asset.GetComponent<DynamicDrivingLine>() == null)
                            throw new InvalidOperationException("Missing DynamicDrivingLine in " + prefabPath);
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                        Undo.RegisterCreatedObjectUndo(instance, "Install missing driving line");
                        line = instance.GetComponent<DynamicDrivingLine>();
                    }
                    var serialized = new SerializedObject(line);
                    FillReference(serialized, "coordinator", Find<RaceCoordinator>(scene));
                    FillReference(serialized, "sessionInstaller", Find<RaceSessionSceneInstaller>(scene));
                    FillReference(serialized, "racingLine", Find<RacingLine>(scene));
                    var sceneRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "RaceSceneRoot");
                    var road = sceneRoot != null ? sceneRoot.transform.Find("Track/Circuit/Road") : null;
                    FillReference(serialized, "road", road != null ? road.GetComponent<MeshCollider>() : null);
                    if (serialized.ApplyModifiedProperties() || created)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(line);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }

            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void FillReference(SerializedObject target, string name, Object value)
        {
            var property = target.FindProperty(name);
            if (property.objectReferenceValue == null && value != null) property.objectReferenceValue = value;
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
    }
}
