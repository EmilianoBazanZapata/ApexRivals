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
                if (shader == null) throw new InvalidOperationException("Driving line shader is missing.");
                var materialPath = Folder + "/Art/DrivingLine.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                var root = new GameObject("DrivingLine");
                try
                {
                    var line = root.AddComponent<DynamicDrivingLine>();
                    root.GetComponent<MeshRenderer>().enabled = false;
                    var serialized = new SerializedObject(line);
                    serialized.FindProperty("lineMaterial").objectReferenceValue = material;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + "/DrivingLine.prefab");
                }
                finally { Object.DestroyImmediate(root); }
                foreach (var name in new[] { "Race", "RaceDunes", "RaceSuzuka" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + name + ".unity");
                    var previous = Find<DynamicDrivingLine>(scene);
                    if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/DrivingLine.prefab"), scene);
                    Undo.RegisterCreatedObjectUndo(instance, "Install driving line");
                    var serialized = new SerializedObject(instance.GetComponent<DynamicDrivingLine>());
                    serialized.FindProperty("coordinator").objectReferenceValue = Find<RaceCoordinator>(scene);
                    serialized.FindProperty("sessionInstaller").objectReferenceValue = Find<RaceSessionSceneInstaller>(scene);
                    serialized.FindProperty("racingLine").objectReferenceValue = Find<RacingLine>(scene);
                    var sceneRoot = scene.GetRootGameObjects().First(r => r.name == "RaceSceneRoot");
                    var road = sceneRoot.transform.Find("Track/Circuit/Road").GetComponent<MeshCollider>();
                    serialized.FindProperty("road").objectReferenceValue = road;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<DynamicDrivingLine>());
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
    }
}
