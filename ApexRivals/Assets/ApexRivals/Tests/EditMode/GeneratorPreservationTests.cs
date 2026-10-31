using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ApexRivals.AI.Runtime;
using ApexRivals.DrivingAssistance.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Tracks.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ApexRivals.Tests.EditMode.EditorGenerators
{
    public sealed class GeneratorPreservationTests
    {
        private string _folder;
        private string _name;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _name = "__GeneratorPreservation_" + Guid.NewGuid().ToString("N");
            _folder = "Assets/" + _name;
            AssetDatabase.CreateFolder("Assets", _name);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            AssetDatabase.DeleteAsset(_folder);
            AssetDatabase.DeleteAsset("Assets/ApexRivals/Art/VFX/Materials/" + _name + ".mat");
            AssetDatabase.DeleteAsset("Assets/ApexRivals/Art/VFX/Prefabs/" + _name + ".prefab");
        }

        [Test]
        public void ExistingVfxMaterialKeepsItsShaderAndCustomValues()
        {
            var shader = Shader.Find("ApexRivals/VFX/Faceted Particle");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetFloat("_LightInfluence", 0.23f);
            var path = "Assets/ApexRivals/Art/VFX/Materials/" + _name + ".mat";
            AssetDatabase.CreateAsset(material, path);
            var before = File.ReadAllBytes(path);
            var initialized = false;
            var result = Invoke("VFX.RaceVfxAssetBuilder", "Material", _name, "Missing/Shader", new Action<Material>(m => initialized = true));
            Assert.That(result, Is.EqualTo(material));
            Assert.That(initialized, Is.False);
            Assert.That(material.GetFloat("_LightInfluence"), Is.EqualTo(0.23f));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [Test]
        public void ExistingParticlePrefabKeepsAuthoredSettings()
        {
            _root = new GameObject(_name);
            var particles = _root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startSize = 7.3f;
            main.maxParticles = 17;
            _root.transform.localScale = new Vector3(2, 3, 4);
            var path = "Assets/ApexRivals/Art/VFX/Prefabs/" + _name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(_root, path);
            var before = File.ReadAllBytes(path);
            var result = (ParticleSystem)Invoke("VFX.RaceVfxAssetBuilder", "Prefab", _name, null, null, 2);
            Assert.That(result.main.startSize.constant, Is.EqualTo(7.3f));
            Assert.That(result.main.maxParticles, Is.EqualTo(17));
            Assert.That(result.transform.localScale, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [Test]
        public void ExistingMeshIsNotReplacedByGeneratedGeometry()
        {
            var type = EditorType("Track.CircuitTrackBuilder");
            var field = type.GetField("_generatedDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = field.GetValue(null);
            var existing = new Mesh { name = "CustomMesh", vertices = new[] { Vector3.one } };
            AssetDatabase.CreateAsset(existing, _folder + "/CustomMesh.asset");
            var generated = new Mesh { name = "CustomMesh", vertices = new[] { Vector3.zero, Vector3.up } };
            try
            {
                field.SetValue(null, _folder);
                var result = Invoke("Track.CircuitTrackBuilder", "SaveMesh", generated);
                Assert.That(result, Is.SameAs(existing));
                Assert.That(existing.vertices, Is.EqualTo(new[] { Vector3.one }));
            }
            finally
            {
                field.SetValue(null, previous);
                if (generated != null) Object.DestroyImmediate(generated);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExistingPreviewKeepsItsBytesAndImporterEvenWhenNotASprite(bool sprite)
        {
            var texture = new Texture2D(2, 2);
            var path = _folder + "/Track_Preview.png";
            try { File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = 64;
            importer.SaveAndReimport();
            var bytes = File.ReadAllBytes(path);
            var meta = File.ReadAllBytes(path + ".meta");
            var result = Invoke("Track.TrackSelectDataBaker", "CapturePreview", _folder + "/Track.asset", new SpawnPose(Vector3.zero, Quaternion.identity));
            Assert.That(result != null, Is.EqualTo(sprite));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(meta));
        }

        [Test]
        public void CompleteTrackDefinitionIsPreservedWithoutOpeningItsScene()
        {
            var definition = ScriptableObject.CreateInstance<TrackDefinition>();
            var texture = new Texture2D(2, 2);
            var preview = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("sceneName").stringValue = "DoesNotExist";
                serialized.FindProperty("lengthMeters").floatValue = 123.4f;
                serialized.FindProperty("minimapPoints").arraySize = 1;
                serialized.FindProperty("minimapPoints").GetArrayElementAtIndex(0).vector2Value = new Vector2(0.2f, 0.7f);
                serialized.FindProperty("preview").objectReferenceValue = preview;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var before = EditorJsonUtility.ToJson(definition);
                var result = Invoke("Track.TrackSelectDataBaker", "Bake", definition, _folder + "/Track.asset");
                Assert.That(result.ToString(), Does.Contain("preserved"));
                Assert.That(EditorJsonUtility.ToJson(definition), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(definition); Object.DestroyImmediate(preview); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MissingMaterialIsInitializedOnlyOnFirstCreation()
        {
            var count = 0;
            Action<Material> initialize = m => { count++; m.SetFloat("_LightInfluence", 0.41f); };
            var material = (Material)Invoke("VFX.RaceVfxAssetBuilder", "Material", _name, "ApexRivals/VFX/Faceted Particle", initialize);
            material.SetFloat("_LightInfluence", 0.77f);
            Invoke("VFX.RaceVfxAssetBuilder", "Material", _name, "ApexRivals/VFX/Faceted Particle", initialize);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(material.GetFloat("_LightInfluence"), Is.EqualTo(0.77f));
        }

        [Test]
        public void DrivingLineBindingKeepsAuthoredReferenceAndVisualSettings()
        {
            _root = new GameObject(_name);
            var line = _root.AddComponent<DynamicDrivingLine>();
            var authored = _root.AddComponent<RacingLine>();
            var other = new GameObject("Other");
            other.transform.SetParent(_root.transform);
            var replacement = other.AddComponent<RacingLine>();
            var serialized = new SerializedObject(line);
            serialized.FindProperty("racingLine").objectReferenceValue = authored;
            serialized.FindProperty("safeColor").colorValue = Color.magenta;
            serialized.FindProperty("visibleDistance").floatValue = 73f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized = new SerializedObject(line);
            Invoke("Track.DrivingLineBuilder", "FillReference", serialized, "racingLine", replacement);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(serialized.FindProperty("racingLine").objectReferenceValue, Is.EqualTo(authored));
            Assert.That(serialized.FindProperty("safeColor").colorValue, Is.EqualTo(Color.magenta));
            Assert.That(serialized.FindProperty("visibleDistance").floatValue, Is.EqualTo(73f));
        }

        [Test]
        public void ExistingCircuitIsPreservedBeforeReadingTheGenerationLayout()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("RaceSceneRoot");
                var track = new GameObject("Track");
                track.transform.SetParent(root.transform);
                var circuit = new GameObject("Circuit");
                circuit.transform.SetParent(track.transform);
                circuit.transform.localPosition = new Vector3(11, 22, 33);
                var environment = new GameObject("Environment");
                environment.transform.SetParent(root.transform);
                root.AddComponent<RacingLine>();
                root.AddComponent<StartingGrid>();
                var path = _folder + "/Circuit.unity";
                EditorSceneManager.SaveScene(scene, path);
                var bytes = File.ReadAllBytes(path);
                var type = EditorType("Track.CircuitTrackBuilder");
                var profileType = type.GetNestedType("CircuitProfile");
                var profile = Activator.CreateInstance(profileType, path, "MissingLayout.json", _folder);
                var result = type.GetMethod("Rebuild", new[] { profileType }).Invoke(null, new[] { profile });
                Assert.That(result.ToString(), Does.Contain("preserved"));
                Assert.That(circuit.transform.localPosition, Is.EqualTo(new Vector3(11, 22, 33)));
                Assert.That(scene.isDirty, Is.False);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static Type EditorType(string name) => Type.GetType("ApexRivals.Editor." + name + ", ApexRivals.Editor", true);
        private static object Invoke(string type, string method, params object[] args) =>
            EditorType(type).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }
}
