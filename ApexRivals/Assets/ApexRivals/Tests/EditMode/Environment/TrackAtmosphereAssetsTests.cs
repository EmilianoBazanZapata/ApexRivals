using ApexRivals.Environment.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Environment
{
    public sealed class TrackAtmosphereAssetsTests
    {
        private const string Folder = "Assets/ApexRivals/Environment/";

        [TestCase("Race")]
        [TestCase("RaceDunes")]
        [TestCase("RaceSuzuka")]
        public void PrefabsShareCrowdGeometryAndStayOutsideVehiclePhysics(string scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Prefabs/" + scene + "Atmosphere.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(), Is.Empty);
            var atmosphere = prefab.GetComponent<TrackAtmosphere>();
            Assert.That(atmosphere.SpectatorCount, Is.GreaterThan(300));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "Art/CrowdSection.asset");
            foreach (var group in prefab.GetComponentsInChildren<LODGroup>())
                Assert.That(group.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(mesh.vertexCount, Is.LessThan(20000));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "Art/TracksideMotion.shader");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [TestCase("Crowd")]
        [TestCase("Wind")]
        [TestCase("Rain")]
        public void AmbientLoopsAreAudiblePreloadedAndHaveMatchingSeams(string kind)
        {
            var path = Folder + "Audio/" + kind + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.length, Is.EqualTo(8).Within(0.01f));
            Assert.That(((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings.preloadAudioData, Is.True);
            var samples = new float[clip.samples];
            Assert.That(clip.GetData(samples, 0), Is.True);
            var energy = 0f;
            foreach (var sample in samples) energy += sample * sample;
            Assert.That(energy / samples.Length, Is.GreaterThan(0.0001f));
            Assert.That(Mathf.Abs(samples[0] - samples[samples.Length - 1]), Is.LessThan(0.001f));
        }
    }
}
