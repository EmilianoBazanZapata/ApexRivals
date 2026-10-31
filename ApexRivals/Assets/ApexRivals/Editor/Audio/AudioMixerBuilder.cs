using System;
using System.Collections;
using System.Reflection;
using ApexRivals.Audio.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace ApexRivals.Editor.Audio
{
    /// <summary>
    /// Creates the game's AudioMixer: Master with one group per <see cref="AudioBus"/> (Music, UI, Vehicles, Ambience)
    /// and the group volumes <see cref="GameAudio"/> drives from the Settings sliders exposed as MasterVolume,
    /// MusicVolume, UiVolume and VehiclesVolume (Ambience follows Master only). Unity has no public API to add mixer
    /// groups or expose parameters, so this goes through the editor's internal AudioMixerController by reflection and
    /// fails with a clear message if Unity changes it. Does nothing if the mixer already exists.
    /// </summary>
    public static class AudioMixerBuilder
    {
        public const string MixerPath = "Assets/ApexRivals/Audio/Mixers/ApexRivals.mixer";

        /// <summary>Exposed volume parameter per group; null where the group has no slider of its own.</summary>
        private static readonly (string group, string parameter)[] Groups =
        {
            ("Music", GameAudio.MusicVolumeParameter),
            ("UI", GameAudio.UiVolumeParameter),
            ("Vehicles", GameAudio.VehiclesVolumeParameter),
            ("Ambience", null)
        };

        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [MenuItem("ApexRivals/Audio/Create Audio Mixer")]
        public static void CreateFromMenu()
        {
            try
            {
                Debug.Log(Create());
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not create the audio mixer: " + exception.Message);
            }
        }

        /// <summary>The mixer group a bus plays through, or null before the mixer exists.</summary>
        public static AudioMixerGroup FindGroup(AudioBus bus)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) return null;
            var name = bus switch
            {
                AudioBus.Music => "Music",
                AudioBus.Ui => "UI",
                AudioBus.Ambience => "Ambience",
                _ => "Vehicles"
            };
            foreach (var group in mixer.FindMatchingGroups(name))
            {
                if (group.name == name) return group;
            }

            return null;
        }

        public static string Create()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
            {
                return "Audio mixer already exists: " + MixerPath;
            }

            if (!AssetDatabase.IsValidFolder("Assets/ApexRivals/Audio/Mixers"))
            {
                AssetDatabase.CreateFolder("Assets/ApexRivals/Audio", "Mixers");
            }

            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var controllerType = Require(editorAssembly.GetType("UnityEditor.Audio.AudioMixerController"), "AudioMixerController");
            var groupType = Require(editorAssembly.GetType("UnityEditor.Audio.AudioMixerGroupController"), "AudioMixerGroupController");
            var groupPathType = Require(editorAssembly.GetType("UnityEditor.Audio.AudioGroupParameterPath"), "AudioGroupParameterPath");
            var exposedType = Require(editorAssembly.GetType("UnityEditor.Audio.ExposedAudioParameter"), "ExposedAudioParameter");

            var mixer = Require(controllerType.GetMethod("CreateMixerControllerAtPath", Members), "CreateMixerControllerAtPath")
                .Invoke(null, new object[] { MixerPath });
            Require(mixer, "the new mixer");
            var master = Require(controllerType.GetProperty("masterGroup", Members), "masterGroup").GetValue(mixer);
            var createGroup = Require(controllerType.GetMethod("CreateNewGroup", Members), "CreateNewGroup");
            var addChild = Require(controllerType.GetMethod("AddChildToParent", Members), "AddChildToParent");
            var addToView = controllerType.GetMethod("AddGroupToCurrentView", Members);
            var addExposed = Require(controllerType.GetMethod("AddExposedParameter", Members), "AddExposedParameter");
            var volumeGuid = Require(groupType.GetMethod("GetGUIDForVolume", Members), "GetGUIDForVolume");
            var exposedProperty = Require(controllerType.GetProperty("exposedParameters", Members), "exposedParameters");

            // Expose a group's volume, then name the new entry (Unity gives it a placeholder name).
            void Expose(object group, string name)
            {
                var guid = volumeGuid.Invoke(group, null);
                addExposed.Invoke(mixer, new[] { Activator.CreateInstance(groupPathType, group, guid) });
                var exposed = (IList)exposedProperty.GetValue(mixer);
                var guidField = exposedType.GetField("guid", Members);
                var nameField = exposedType.GetField("name", Members);
                for (var i = 0; i < exposed.Count; i++)
                {
                    var entry = exposed[i];
                    if (!Equals(guidField.GetValue(entry), guid)) continue;
                    nameField.SetValue(entry, name);
                    exposed[i] = entry;
                }

                exposedProperty.SetValue(mixer, exposed);
            }

            Expose(master, GameAudio.MasterVolumeParameter);
            foreach (var (groupName, parameter) in Groups)
            {
                var group = createGroup.Invoke(mixer, new object[] { groupName, false });
                addChild.Invoke(mixer, new[] { group, master });
                addToView?.Invoke(mixer, new[] { group });
                if (parameter != null) Expose(group, parameter);
            }

            EditorUtility.SetDirty((UnityEngine.Object)mixer);
            AssetDatabase.SaveAssets();
            return "Audio mixer created: " + MixerPath + " (Master > Music, UI, Vehicles, Ambience). Now run ApexRivals/Audio/Set Up Audio Sources.";
        }

        private static T Require<T>(T value, string what) where T : class
        {
            if (value == null) throw new InvalidOperationException("Unity's internal " + what + " was not found (Unity version changed?).");
            return value;
        }
    }
}
