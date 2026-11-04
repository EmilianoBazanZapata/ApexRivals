using ApexRivals.Audio.Runtime;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Editor.Audio
{
    /// <summary>The kinds of AudioSource the game uses; each gets the same 2D/3D settings everywhere.</summary>
    internal enum AudioSourcePreset
    {
        /// <summary>2D looping music; keeps playing through the pause menu.</summary>
        Music,
        /// <summary>2D one-shot menu cues; keep playing through the pause menu.</summary>
        Ui,
        /// <summary>3D looping engine, heard from far away, wide and with a fly-by doppler.</summary>
        Engine,
        /// <summary>3D looping car effect (tyre skid, nitro).</summary>
        VehicleLoop,
        /// <summary>3D one-shot car effect (collision hits).</summary>
        VehicleImpact,
        /// <summary>2D looping source for the engine sound bench.</summary>
        Bench
    }

    /// <summary>
    /// Sets up AudioSources by kind and routes them through an <see cref="AudioBus"/> with an
    /// <see cref="AudioBusSource"/>. Car sounds use one custom distance curve: full level next to the car, about half
    /// at 20 m, a whisper at 100 m and silent at the maximum distance (a logarithmic rolloff never reaches silence, so
    /// far-away cars would keep using voices).
    /// </summary>
    internal static class AudioSourcePresets
    {
        /// <summary>
        /// Creates the child <paramref name="name"/> under <paramref name="parent"/> with a set-up, routed source. If it
        /// already exists it is returned as it is (only a missing mixer output is filled).
        /// </summary>
        public static AudioBusSource CreateChild(
            Transform parent,
            string name,
            Vector3 localPosition,
            AudioSourcePreset preset,
            AudioBus bus,
            float level,
            bool pauseWithGame,
            AudioClip clip = null)
        {
            var existing = parent.Find(name);
            var routed = existing != null ? existing.GetComponent<AudioBusSource>() : null;
            if (routed != null)
            {
                // Already set up: its clip, 3D settings and level are edited in the Inspector now.
                FillMixerGroup(routed.Source, routed.Bus);
                return routed;
            }

            var child = existing;
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
                child.localPosition = localPosition;
                child.localRotation = Quaternion.identity;
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null)
            {
                source = child.gameObject.AddComponent<AudioSource>();
                Apply(source, preset);
                source.clip = clip;
            }
            return Route(source, bus, level, pauseWithGame);
        }

        public static void Apply(AudioSource source, AudioSourcePreset preset)
        {
            source.playOnAwake = false;
            source.mute = false;
            source.pitch = 1f;
            source.volume = 0f;
            source.bypassEffects = false;
            source.reverbZoneMix = 1f;
            switch (preset)
            {
                case AudioSourcePreset.Music:
                    TwoD(source, loop: true, priority: 0);
                    source.ignoreListenerPause = true;
                    break;
                case AudioSourcePreset.Ui:
                    TwoD(source, loop: false, priority: 8);
                    source.ignoreListenerPause = true;
                    break;
                case AudioSourcePreset.Bench:
                    TwoD(source, loop: true, priority: 0);
                    break;
                case AudioSourcePreset.Engine:
                    ThreeD(source, loop: true, priority: 64, maxDistance: 260f, spread: 60f, doppler: 0.6f);
                    break;
                case AudioSourcePreset.VehicleLoop:
                    ThreeD(source, loop: true, priority: 128, maxDistance: 160f, spread: 90f, doppler: 0.25f);
                    break;
                case AudioSourcePreset.VehicleImpact:
                    ThreeD(source, loop: false, priority: 100, maxDistance: 160f, spread: 30f, doppler: 0f);
                    break;
            }
        }

        /// <summary>
        /// Adds the <see cref="AudioBusSource"/> that routes <paramref name="source"/> through <paramref name="bus"/>.
        /// A source that is already routed keeps its bus, level and pause setting; only a missing mixer output is filled.
        /// </summary>
        public static AudioBusSource Route(AudioSource source, AudioBus bus, float level, bool pauseWithGame)
        {
            var routed = source.GetComponent<AudioBusSource>();
            if (routed != null)
            {
                FillMixerGroup(source, routed.Bus);
                return routed;
            }

            // Through the bus's mixer group once the mixer exists.
            FillMixerGroup(source, bus);
            routed = source.gameObject.AddComponent<AudioBusSource>();
            var refs = new SerializedObject(routed);
            refs.FindProperty("bus").enumValueIndex = (int)bus;
            refs.FindProperty("level").floatValue = level;
            refs.FindProperty("pauseWithGame").boolValue = pauseWithGame;
            refs.ApplyModifiedPropertiesWithoutUndo();
            return routed;
        }

        private static void FillMixerGroup(AudioSource source, AudioBus bus)
        {
            if (source != null && source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = AudioMixerBuilder.FindGroup(bus);
        }

        private static void TwoD(AudioSource source, bool loop, int priority)
        {
            source.loop = loop;
            source.priority = priority;
            source.spatialBlend = 0f;
            source.spread = 0f;
            source.dopplerLevel = 0f;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
        }

        private static void ThreeD(AudioSource source, bool loop, int priority, float maxDistance, float spread, float doppler)
        {
            source.loop = loop;
            source.priority = priority;
            source.ignoreListenerPause = false;
            source.spatialBlend = 1f;
            source.spread = spread;
            source.dopplerLevel = doppler;
            source.bypassListenerEffects = false;
            source.bypassReverbZones = false;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, DistanceCurve(maxDistance));
        }

        /// <summary>Volume by distance (the curve's x is distance / maxDistance).</summary>
        private static AnimationCurve DistanceCurve(float maxDistance)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(3f / maxDistance, 1f),
                new Keyframe(20f / maxDistance, 0.5f),
                new Keyframe(50f / maxDistance, 0.2f),
                new Keyframe(100f / maxDistance, 0.06f),
                new Keyframe(1f, 0f));
            for (var i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
