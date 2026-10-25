using System.IO;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Editor.Track
{
    internal static class TracksideAudioBuilder
    {
        /// <summary>Original stylized noise/voice textures, not recordings or spoken dialogue.</summary>
        public static void Write(string path, string kind)
        {
            const int rate = 22050;
            const int duration = 8;
            var samples = new float[rate * duration];
            var random = new System.Random(kind == "Crowd" ? 734 : kind == "Rain" ? 991 : 163);
            var noise = new float[samples.Length];
            for (var i = 0; i < noise.Length; i++) noise[i] = (float)random.NextDouble() * 2 - 1;
            var low = 0f;
            // A warmup around the same periodic noise establishes a circular filter history.
            for (var i = 0; i < samples.Length * 2; i++)
            {
                var index = i % samples.Length;
                var t = (float)index / rate;
                low += (noise[index] - low) * (kind == "Wind" ? 0.025f : 0.15f);
                var value = kind == "Rain" ? (noise[index] - low) * 0.3f : low * (0.7f + 0.25f * Mathf.Sin(2 * Mathf.PI * t / duration));
                if (kind == "Crowd")
                {
                    value = low * 0.45f;
                    for (var voice = 0; voice < 9; voice++)
                    {
                        var envelope = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * t * (voice % 3 + 1) / duration + voice), 4);
                        var phase = 2 * Mathf.PI * (110 + voice * 7) * t + 0.2f * Mathf.Sin(2 * Mathf.PI * t * 3 / duration + voice);
                        value += (Mathf.Sin(phase) * 0.045f + Mathf.Sin(phase * 3) * 0.025f + Mathf.Sin(phase * 5) * 0.012f) * envelope;
                    }
                    // Distant applause and a short, soft whistle within the loop.
                    value += noise[index] * Mathf.Pow(Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * t * 19 / duration)), 18) * 0.12f;
                    value += Mathf.Sin(2 * Mathf.PI * 1850 * t) * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * t / duration), 32) * 0.035f;
                }
                if (i >= samples.Length) samples[index] = value;
            }
            // Match the seam exactly without silencing the whole bed at the wrap.
            for (var i = 0; i < 256; i++)
            {
                var blend = (float)i / 255;
                var index = samples.Length - 256 + i;
                samples[index] = Mathf.Lerp(samples[index], samples[0], blend);
            }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (var sample in samples) writer.Write((short)(Mathf.Clamp(sample, -1, 1) * short.MaxValue));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
