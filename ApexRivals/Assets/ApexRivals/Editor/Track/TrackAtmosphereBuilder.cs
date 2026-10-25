using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Environment.Runtime;
using ApexRivals.Race.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ApexRivals.Editor.Track
{
    public static class TrackAtmosphereBuilder
    {
        public const string Folder = "Assets/ApexRivals/Environment";

        [MenuItem("Apex Rivals/Environment/Rebuild Track Atmosphere")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before rebuilding atmosphere.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder + "/Art");
                Directory.CreateDirectory(Folder + "/Audio");
                Directory.CreateDirectory(Folder + "/Prefabs");
                AssetDatabase.Refresh();
                var builder = new TracksideMeshBuilder();
                SaveMesh(builder.Crowd(), "CrowdSection");
                var spectators = builder.Spectators;
                SaveMesh(new TracksideMeshBuilder().Flag(), "Flag");
                SaveMesh(new TracksideMeshBuilder().Pole(), "FlagPole");
                SaveMaterial("Crowd", new Vector4(1, 0.3f, 0, 0), 0.12f, CullMode.Back);
                SaveMaterial("Flag", new Vector4(0, 0, 1, 0), 0.55f, CullMode.Off);
                foreach (var kind in new[] { "Crowd", "Wind", "Rain" }) TracksideAudioBuilder.Write(Folder + "/Audio/" + kind + ".wav", kind);
                AssetDatabase.SaveAssets();

                foreach (var name in new[] { "Race", "RaceDunes", "RaceSuzuka" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + name + ".unity");
                    var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                    var stands = transforms.Where(t => t.name == "grandStandCovered" && t.parent != null && t.parent.name == "CircuitProps").ToArray();
                    if (stands.Length == 0) throw new InvalidOperationException("No grandstands found in " + name);
                    var previous = Find<TrackAtmosphere>(scene);
                    if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
                    var root = new GameObject("TrackAtmosphere");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    var animated = new List<Renderer>();
                    var sources = new List<AudioSource>();
                    for (var i = 0; i < stands.Length; i++)
                    {
                        var section = new GameObject("CrowdSection_" + i);
                        section.transform.SetParent(root.transform, false);
                        section.transform.SetPositionAndRotation(stands[i].position, stands[i].rotation);
                        section.transform.localScale = stands[i].lossyScale;
                        var crowd = MeshObject("Spectators", section.transform, "CrowdSection", "Crowd");
                        animated.Add(crowd);
                        var lod = section.AddComponent<LODGroup>();
                        lod.SetLODs(new[] { new LOD(0.035f, new Renderer[] { crowd }) });
                        lod.RecalculateBounds();
                        if (i % 3 != 0) continue;
                        var source = Loop("CrowdAmbience", section.transform, "Crowd", true);
                        source.transform.localPosition = new Vector3(0, 4, -2);
                        sources.Add(source);
                        var flag = new GameObject("TracksideFlag");
                        flag.transform.SetParent(root.transform, false);
                        // Forecourt: beyond the safety barrier, clear of the roof and spectator seats.
                        flag.transform.SetPositionAndRotation(stands[i].TransformPoint(new Vector3(-5.5f, 0, -6.5f)), stands[i].rotation);
                        MeshObject("Pole", flag.transform, "FlagPole", "Crowd");
                        animated.Add(MeshObject("Fabric", flag.transform, "Flag", "Flag"));
                    }
                    var weather = Loop(name == "RaceSuzuka" ? "RainAmbience" : "WindAmbience", root.transform,
                        name == "RaceSuzuka" ? "Rain" : "Wind", false);
                    var atmosphere = root.AddComponent<TrackAtmosphere>();
                    var serialized = new SerializedObject(atmosphere);
                    SetArray(serialized, "animatedRenderers", animated.ToArray());
                    SetArray(serialized, "crowdSources", sources.ToArray());
                    serialized.FindProperty("weatherSource").objectReferenceValue = weather;
                    serialized.FindProperty("spectatorCount").intValue = stands.Length * spectators;
                    serialized.FindProperty("weatherLevel").floatValue = name == "RaceSuzuka" ? 0.2f : 0.12f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var path = Folder + "/Prefabs/" + name + "Atmosphere.prefab";
                    PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
                    Undo.RegisterCreatedObjectUndo(root, "Add track atmosphere");
                    serialized = new SerializedObject(atmosphere);
                    serialized.FindProperty("coordinator").objectReferenceValue = Find<RaceCoordinator>(scene);
                    serialized.FindProperty("sessionInstaller").objectReferenceValue = Find<RaceSessionSceneInstaller>(scene);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(atmosphere);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log(name + ": " + stands.Length + " populated stands, " + stands.Length * spectators + " spectators, " + sources.Count + " crowd emitters.");
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static MeshRenderer MeshObject(string name, Transform parent, string mesh, string material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Art/" + mesh + ".asset");
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Art/" + material + ".mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static AudioSource Loop(string name, Transform parent, string clip, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/" + clip + ".wav");
            source.playOnAwake = false; source.loop = true;
            source.spatialBlend = spatial ? 1 : 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 8; source.maxDistance = 75;
            source.dopplerLevel = 0;
            source.priority = 180;
            return source;
        }

        private static void SaveMesh(Mesh mesh, string name)
        {
            var path = Folder + "/Art/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); }
        }
        private static void SaveMaterial(string name, Vector4 axis, float amount, CullMode cull)
        {
            var shader = Shader.Find("ApexRivals/Environment/Trackside Motion");
            if (shader == null) throw new InvalidOperationException("Trackside motion shader is missing.");
            var path = Folder + "/Art/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.SetVector("_MotionAxis", axis); material.SetFloat("_MotionAmount", amount); material.SetFloat("_Cull", (float)cull);
            EditorUtility.SetDirty(material);
        }
        private static void SetArray<T>(SerializedObject target, string name, T[] objects) where T : Object
        {
            var property = target.FindProperty(name);
            property.arraySize = objects.Length;
            for (var i = 0; i < objects.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
    }
}
