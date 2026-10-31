using System.Linq;
using System.Text;
using ApexRivals.Audio.Runtime;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Editor.Audio
{
    /// <summary>
    /// Sets up every car prefab's sounds as AudioSources on the car body (the object with the Rigidbody, so they move
    /// with the car), all 3D and routed through the Vehicles bus:
    /// <list type="bullet">
    /// <item>EngineSound: the <see cref="ProceduralEngineSynth"/> with the <see cref="EngineSoundProfile"/> that matches
    /// the model, so no two cars sound alike; driven by <see cref="VehicleEngineAudio"/> on the prefab root.</item>
    /// <item>TyreSkid, Nitro and Impact: driven by <see cref="VehicleEffectsAudio"/> on the body.</item>
    /// </list>
    /// Re-running only fills in what a car is missing; sources, profiles and levels already set in the Inspector stay.
    /// Clips come from <see cref="AudioClipBaker"/>, profiles from <see cref="EngineSoundProfileBuilder"/>.
    /// </summary>
    public static class VehicleEngineSoundAssigner
    {
        private const string PrefabFolder = "Assets/ApexRivals/Vehicle/Prefabs/";
        private const string ProfileFolder = "Assets/ApexRivals/Audio/Engines/";

        /// <summary>Car prefab - engine profile. Every car gets a different engine.</summary>
        private static readonly (string prefab, string profile)[] Assignments =
        {
            ("Vehicle_Aduo", "V10"),            // Aduo P8: mid-engined V10 supercar
            ("Vehicle_Borshe911", "Flat6"),     // Borshe 911: rear-mounted boxer six
            ("Vehicle_Fehhahi", "V8FlatPlane"), // Fehhahi: high-revving flat-plane V8
            ("Vehicle_Fernedenz", "V8Biturbo"), // Fernedenz: twin-turbo cross-plane V8
            ("Vehicle_Massalini", "V6Biturbo"), // Massalini: twin-turbo V6
            ("Vehicle_Striker", "V8CrossPlane"),// Tort Mustang: muscle-car cross-plane V8
            ("Vehicle_Sunaru", "Boxer4Turbo"),  // Sunaru Arteza: turbo boxer four
            ("Vehicle_Vanguard", "I6")          // BTW S1: straight six
        };

        [MenuItem("ApexRivals/Audio/Assign Engine Sounds To Cars")]
        public static void AssignFromMenu()
        {
            Debug.Log(Assign());
        }

        public static string Assign()
        {
            EngineSoundProfileBuilder.CreateMissingProfiles();
            var skidClip = AudioClipBaker.Load(AudioClipBaker.TireSkid);
            var nitroClip = AudioClipBaker.Load(AudioClipBaker.Nitro);
            var impactClips = AudioClipBaker.Impacts.Select(AudioClipBaker.Load).Where(clip => clip != null).ToArray();
            if (skidClip == null || nitroClip == null || impactClips.Length == 0)
            {
                return "Car sound clips are missing; run ApexRivals/Audio/Set Up Audio Sources.";
            }

            var report = new StringBuilder();
            foreach (var (prefabName, profileName) in Assignments)
            {
                var prefabPath = PrefabFolder + prefabName + ".prefab";
                var profile = AssetDatabase.LoadAssetAtPath<EngineSoundProfile>(ProfileFolder + profileName + ".asset");
                if (profile == null)
                {
                    report.AppendLine(prefabName + ": profile " + profileName + " not found");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var body = root.GetComponentInChildren<Rigidbody>();
                    var host = body != null ? body.transform : root.transform;

                    var engine = AudioSourcePresets.CreateChild(host, "EngineSound", new Vector3(0f, 0.5f, 0f),
                        AudioSourcePreset.Engine, AudioBus.Vehicles, 0.85f, true);
                    var synth = engine.GetComponent<ProceduralEngineSynth>();
                    if (synth == null)
                    {
                        synth = engine.gameObject.AddComponent<ProceduralEngineSynth>();
                        var synthRefs = new SerializedObject(synth);
                        synthRefs.FindProperty("volume").floatValue = 1f;
                        synthRefs.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // The model's engine is only a starting point: a profile chosen in the Inspector is kept.
                    FillEmpty(synth, "profile", profile);

                    var engineAudio = root.GetComponent<VehicleEngineAudio>();
                    if (engineAudio == null) engineAudio = root.AddComponent<VehicleEngineAudio>();
                    FillEmpty(engineAudio, "synth", synth);

                    var skid = AudioSourcePresets.CreateChild(host, "TyreSkid", new Vector3(0f, 0.2f, -0.6f),
                        AudioSourcePreset.VehicleLoop, AudioBus.Vehicles, 0.55f, true, skidClip);
                    var nitro = AudioSourcePresets.CreateChild(host, "Nitro", new Vector3(0f, 0.4f, -2f),
                        AudioSourcePreset.VehicleLoop, AudioBus.Vehicles, 0.6f, true, nitroClip);
                    var impact = AudioSourcePresets.CreateChild(host, "Impact", new Vector3(0f, 0.5f, 0f),
                        AudioSourcePreset.VehicleImpact, AudioBus.Vehicles, 0.9f, true);

                    var effects = host.GetComponent<VehicleEffectsAudio>();
                    if (effects == null) effects = host.gameObject.AddComponent<VehicleEffectsAudio>();
                    FillEmpty(effects, "skid", skid);
                    FillEmpty(effects, "nitro", nitro);
                    FillEmpty(effects, "impact", impact);
                    var effectRefs = new SerializedObject(effects);
                    var clips = effectRefs.FindProperty("impactClips");
                    if (clips.arraySize == 0)
                    {
                        clips.arraySize = impactClips.Length;
                        for (var i = 0; i < impactClips.Length; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = impactClips[i];
                        effectRefs.ApplyModifiedPropertiesWithoutUndo();
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    report.AppendLine(prefabName + ": " + profile.DisplayName + " on " + host.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return report.ToString();
        }

        /// <summary>Sets the reference <paramref name="field"/> only while it is empty, so choices made in the Inspector stay.</summary>
        private static void FillEmpty(Object target, string field, Object value)
        {
            var refs = new SerializedObject(target);
            var slot = refs.FindProperty(field);
            if (slot == null || slot.objectReferenceValue != null) return;
            slot.objectReferenceValue = value;
            refs.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
