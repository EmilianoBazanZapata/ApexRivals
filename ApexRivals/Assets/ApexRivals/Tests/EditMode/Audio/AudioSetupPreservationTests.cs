using System;
using System.IO;
using System.Reflection;
using ApexRivals.Audio.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace ApexRivals.Tests.EditMode.Audio
{
    public sealed class AudioSetupPreservationTests
    {
        private string _folder;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__AudioSetupPreservation_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (!string.IsNullOrEmpty(_folder)) AssetDatabase.DeleteAsset(_folder);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingClipIsCreatedOnceAndExistingAudioAndImporterArePreserved(bool music)
        {
            var path = _folder + "/Custom.wav";
            var generated = 0;
            Func<float[]> samples = () =>
            {
                generated++;
                var data = new float[800];
                for (var i = 0; i < data.Length; i++) data[i] = Mathf.Sin(i * 0.1f) * 0.2f;
                return data;
            };

            Assert.That(WriteIfMissing(path, samples, music), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(path), Is.Not.Null);
            Assert.That(generated, Is.EqualTo(1));

            // Simulate an authored replacement and custom importer settings, rather than only baking twice.
            var replacement = File.ReadAllBytes(path);
            replacement[replacement.Length - 1] ^= 1;
            File.WriteAllBytes(path, replacement);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = false;
            importer.loadInBackground = !music;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.31f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var metaBefore = File.ReadAllBytes(path + ".meta");

            Assert.That(WriteIfMissing(path, samples, music), Is.False);
            Assert.That(generated, Is.EqualTo(1), "Existing clips must not even run the generator.");
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(replacement), "Authored audio must not be truncated or regenerated.");
            Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metaBefore), "Import settings and GUID must remain untouched.");
        }

        [Test]
        public void RoutingAnExistingSourcePreservesItsClipTransformAndAudioSettings()
        {
            _root = new GameObject("AudioSetupPreservation");
            var child = new GameObject("EngineSound");
            child.transform.SetParent(_root.transform, false);
            child.transform.localPosition = new Vector3(3f, 4f, 5f);
            child.transform.localRotation = Quaternion.Euler(12f, 23f, 34f);
            var rotation = child.transform.localRotation;
            var source = child.AddComponent<AudioSource>();
            var clip = AudioClip.Create("AuthoredClip", 800, 1, 8000, false);
            try
            {
                source.clip = clip;
                source.spatialBlend = 0.35f;
                source.pitch = 1.35f;
                source.volume = 0.23f;
                source.loop = false;
                source.playOnAwake = true;
                source.maxDistance = 47f;
                var musicGroup = MixerGroup(AudioBus.Music);
                source.outputAudioMixerGroup = musicGroup;

                var presetType = EditorType("AudioSourcePreset");
                var result = (AudioBusSource)EditorType("AudioSourcePresets").GetMethod("CreateChild").Invoke(null,
                    new object[] { _root.transform, "EngineSound", Vector3.zero, Enum.Parse(presetType, "Engine"), AudioBus.Vehicles, 0.85f, true, null });

                Assert.That(result.Source, Is.SameAs(source));
                Assert.That(source.clip, Is.SameAs(clip));
                Assert.That(source.spatialBlend, Is.EqualTo(0.35f));
                Assert.That(source.pitch, Is.EqualTo(1.35f));
                Assert.That(source.volume, Is.EqualTo(0.23f));
                Assert.That(source.loop, Is.False);
                Assert.That(source.playOnAwake, Is.True);
                Assert.That(source.maxDistance, Is.EqualTo(47f));
                Assert.That(source.outputAudioMixerGroup, Is.SameAs(musicGroup));
                Assert.That(child.transform.localPosition, Is.EqualTo(new Vector3(3f, 4f, 5f)));
                Assert.That(Quaternion.Angle(child.transform.localRotation, rotation), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void CompletingAnExistingRouterUsesItsAuthoredBusAndKeepsItsMixLevel()
        {
            _root = new GameObject("AudioSetupPreservation");
            var source = _root.AddComponent<AudioSource>();
            var routed = _root.AddComponent<AudioBusSource>();
            var serialized = new SerializedObject(routed);
            serialized.FindProperty("bus").enumValueIndex = (int)AudioBus.Ui;
            serialized.FindProperty("level").floatValue = 0.29f;
            serialized.FindProperty("pauseWithGame").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            source.outputAudioMixerGroup = null;

            var result = (AudioBusSource)EditorType("AudioSourcePresets").GetMethod("Route").Invoke(null,
                new object[] { source, AudioBus.Vehicles, 0.85f, true });

            Assert.That(result, Is.SameAs(routed));
            Assert.That(routed.Bus, Is.EqualTo(AudioBus.Ui));
            Assert.That(routed.Level, Is.EqualTo(0.29f));
            Assert.That(source.outputAudioMixerGroup, Is.SameAs(MixerGroup(AudioBus.Ui)));
            serialized.Update();
            Assert.That(serialized.FindProperty("pauseWithGame").boolValue, Is.False);
        }

        private static bool WriteIfMissing(string path, Func<float[]> samples, bool music)
        {
            var method = EditorType("AudioClipBaker").GetMethod("WriteIfMissing", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] { path, samples, 8000, music });
        }

        private static AudioMixerGroup MixerGroup(AudioBus bus)
        {
            var group = (AudioMixerGroup)EditorType("AudioMixerBuilder").GetMethod("FindGroup").Invoke(null, new object[] { bus });
            Assert.That(group, Is.Not.Null, "The authored game mixer must provide the requested bus.");
            return group;
        }

        private static Type EditorType(string name)
        {
            var type = Type.GetType("ApexRivals.Editor.Audio." + name + ", ApexRivals.Editor");
            Assert.That(type, Is.Not.Null, "The editor audio tools must be available.");
            return type;
        }
    }
}
