using System;
using System.IO;
using ApexRivals.Audio.Runtime;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Editor.Audio
{
    /// <summary>
    /// Writes the sounds <see cref="ProceduralAudio"/> generates (tyres, nitro, hits, UI cues, music) to WAV assets in
    /// <see cref="Folder"/>, so AudioSources reference them like any other clip. Existing files and their import
    /// settings belong to the designer and are left untouched.
    /// </summary>
    internal static class AudioClipBaker
    {
        public const string Folder = "Assets/ApexRivals/Audio/Clips";
        public const string TireSkid = Folder + "/TireSkidLoop.wav";
        public const string Nitro = Folder + "/NitroLoop.wav";
        public const string Music = Folder + "/MusicLoop.wav";
        public static readonly string[] Impacts = { Folder + "/Impact0.wav", Folder + "/Impact1.wav", Folder + "/Impact2.wav" };
        /// <summary>One per <see cref="UiSound"/>, in its order.</summary>
        public static readonly string[] UiCues = { Folder + "/UiMove.wav", Folder + "/UiConfirm.wav", Folder + "/UiBack.wav" };

        private const int EffectsSampleRate = 44100;
        private const int MusicSampleRate = 22050;

        public static string Bake()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/ApexRivals/Audio", "Clips");
            }

            var created = 0;
            if (WriteIfMissing(TireSkid, () => ProceduralAudio.CreateTireSkidLoop(EffectsSampleRate), EffectsSampleRate, false)) created++;
            if (WriteIfMissing(Nitro, () => ProceduralAudio.CreateNitroLoop(EffectsSampleRate), EffectsSampleRate, false)) created++;
            for (var i = 0; i < Impacts.Length; i++)
            {
                var variant = i;
                if (WriteIfMissing(Impacts[i], () => ProceduralAudio.CreateImpact(EffectsSampleRate, variant), EffectsSampleRate, false)) created++;
            }

            for (var i = 0; i < UiCues.Length; i++)
            {
                var sound = (UiSound)i;
                if (WriteIfMissing(UiCues[i], () => ProceduralAudio.CreateUiSound(sound, EffectsSampleRate), EffectsSampleRate, false)) created++;
            }

            if (WriteIfMissing(Music, () => ProceduralAudio.CreateMusicLoop(MusicSampleRate), MusicSampleRate, true)) created++;
            var total = 3 + Impacts.Length + UiCues.Length;
            return "Created " + created + " missing clips; preserved " + (total - created) + " existing clips in " + Folder + ".";
        }

        public static AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        private static bool WriteIfMissing(string path, Func<float[]> createSamples, int rate, bool streamedMusic)
        {
            if (File.Exists(path)) return false;

            var samples = createSamples();
            // CreateNew also refuses to truncate a file created between the existence check and this write.
            using (var writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (var sample in samples) writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            // Short effects stay uncompressed in memory for instant, cheap playback; the long music loop is Vorbis.
            settings.loadType = streamedMusic ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = streamedMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = 0.6f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = streamedMusic;
            importer.SaveAndReimport();
            return true;
        }
    }
}
