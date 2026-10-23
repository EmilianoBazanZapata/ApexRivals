using System;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Sounds synthesised in code (mono PCM, -1..1) so the game ships audio without sample files: a seamless engine
    /// loop, short UI cues and a seamless music loop. Deterministic: the same input always gives the same samples.
    /// </summary>
    public static class ProceduralAudio
    {
        /// <summary>Fundamental of <see cref="CreateEngineLoop"/>; whole cycles fit the one-second loop.</summary>
        public const float EngineLoopFrequency = 75f;

        /// <summary>
        /// Engine RPM the engine loop sounds like at pitch 1: a four-stroke four-cylinder fires twice per revolution,
        /// so 75 Hz ≈ 2250 RPM. Pitch = RPM / this.
        /// </summary>
        public const float EngineLoopRpm = EngineLoopFrequency * 30f;

        public const int MusicBeatsPerMinute = 120;
        public const int MusicBars = 8;

        private const float TwoPi = (float)(Math.PI * 2.0);
        private const int TableSize = 2048;

        // ------------------------------------------------------------------ engine

        /// <summary>One-second seamless loop: harmonic-rich exhaust tone, a per-cycle combustion pulse and slight roughness.</summary>
        public static float[] CreateEngineLoop(int sampleRate)
        {
            var samples = new float[sampleRate];
            var random = new Random(7);
            const int harmonics = 12;
            var phases = new float[harmonics + 1];
            for (var h = 1; h <= harmonics; h++)
            {
                phases[h] = (float)random.NextDouble() * TwoPi;
            }

            var period = 1f / EngineLoopFrequency;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var phase = TwoPi * EngineLoopFrequency * t;
                var tone = 0f;
                for (var h = 1; h <= harmonics; h++)
                {
                    tone += (float)Math.Sin(h * phase + phases[h]) / (float)Math.Pow(h, 0.85);
                }

                // Combustion pulse at the start of every cycle: a short rasp that decays within the cycle.
                var inCycle = t % period;
                var pulse = (float)(Math.Exp(-inCycle / (period * 0.18f)) * Math.Sin(TwoPi * EngineLoopFrequency * 7f * inCycle));

                // 25 Hz roughness: an integer number of cycles per second, so the loop stays seamless.
                var roughness = 1f + 0.18f * (float)Math.Sin(TwoPi * 25f * t);
                samples[i] = (float)Math.Tanh(1.4f * (0.55f * tone + 0.6f * pulse) * roughness);
            }

            return Normalize(samples, 0.8f);
        }

        // ------------------------------------------------------------------ UI

        public static float[] CreateUiSound(UiSound sound, int sampleRate)
        {
            switch (sound)
            {
                case UiSound.Confirm:
                    return Normalize(TwoNotes(sampleRate, 784f, 1175f, 0.05f, 0.08f), 0.55f);
                case UiSound.Back:
                    return Normalize(TwoNotes(sampleRate, 659f, 494f, 0.05f, 0.08f), 0.5f);
                default:
                    var samples = new float[(int)(sampleRate * 0.035f)];
                    for (var i = 0; i < samples.Length; i++)
                    {
                        var t = i / (float)sampleRate;
                        var envelope = Math.Min(1f, t / 0.002f) * (float)Math.Exp(-t / 0.009f);
                        samples[i] = envelope * ((float)Math.Sin(TwoPi * 1500f * t) + 0.2f * (float)Math.Sin(TwoPi * 3000f * t));
                    }

                    return Normalize(samples, 0.4f);
            }
        }

        private static float[] TwoNotes(int sampleRate, float first, float second, float firstLength, float secondLength)
        {
            var samples = new float[(int)(sampleRate * (firstLength + secondLength + 0.04f))];
            AddPluck(samples, sampleRate, 0f, firstLength + 0.03f, first, 1f, 0.03f);
            AddPluck(samples, sampleRate, firstLength, secondLength + 0.04f, second, 1f, 0.045f);
            return samples;
        }

        private static void AddPluck(float[] buffer, int sampleRate, float start, float length, float frequency, float amplitude, float decay)
        {
            var from = (int)(start * sampleRate);
            var count = (int)(length * sampleRate);
            for (var n = 0; n < count && from + n < buffer.Length; n++)
            {
                var t = n / (float)sampleRate;
                var envelope = Math.Min(1f, t / 0.003f) * (float)Math.Exp(-t / decay);
                var wave = (float)Math.Sin(TwoPi * frequency * t) + 0.3f * (float)Math.Sin(TwoPi * frequency * 3f * t);
                buffer[from + n] += amplitude * envelope * wave;
            }
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// Seamless 8-bar loop at 120 BPM (16 s): Am - F - C - G with a soft pad, plucked bass, a light arpeggio and
        /// drums. Every voice has died away by the end of its bar, so the end joins the start without a click.
        /// </summary>
        public static float[] CreateMusicLoop(int sampleRate)
        {
            var beat = 60f / MusicBeatsPerMinute;
            var bar = beat * 4f;
            var samples = new float[(int)Math.Round(bar * MusicBars * sampleRate)];
            var pad = CreateTable(6, 1.1f, oddOnly: false);
            var bass = CreateTable(7, 1f, oddOnly: true);
            var random = new Random(1971);

            // Chord tones (MIDI) per bar and bass roots: Am, F, C, G, twice.
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 60, 64 }, new[] { 55, 59, 62 } };
            int[] roots = { 45, 41, 48, 43 };

            for (var b = 0; b < MusicBars; b++)
            {
                var barStart = b * bar;
                var chord = chords[b % chords.Length];
                var root = roots[b % roots.Length];

                foreach (var note in chord)
                {
                    AddTableNote(samples, sampleRate, pad, barStart, bar, Midi(note), 0.11f, attack: 0.35f, release: 0.45f);
                }

                for (var step = 0; step < 8; step++)
                {
                    var note = step % 4 == 2 ? root + 12 : root;
                    AddTableDecay(samples, sampleRate, bass, barStart + step * beat * 0.5f, beat * 0.5f, Midi(note), 0.3f, 0.16f);
                }

                // Sixteenth-note arpeggio an octave up, a little louder in the second half of the loop.
                var arpLevel = b < MusicBars / 2 ? 0.05f : 0.08f;
                for (var step = 0; step < 16; step++)
                {
                    var note = chord[step % chord.Length] + 12 + (step % 8 >= 4 ? 12 : 0);
                    AddSineDecay(samples, sampleRate, barStart + step * beat * 0.25f, beat * 0.25f, Midi(note), arpLevel, 0.07f);
                }

                for (var beatIndex = 0; beatIndex < 4; beatIndex++)
                {
                    var time = barStart + beatIndex * beat;
                    if (beatIndex % 2 == 0)
                    {
                        AddKick(samples, sampleRate, time);
                    }
                    else
                    {
                        AddSnare(samples, sampleRate, time, random);
                    }

                    AddHat(samples, sampleRate, time + beat * 0.5f, random);
                }
            }

            return Normalize(samples, 0.85f);
        }

        private static float Midi(int note)
        {
            return 440f * (float)Math.Pow(2.0, (note - 69) / 12.0);
        }

        private static float[] CreateTable(int harmonics, float rolloff, bool oddOnly)
        {
            var table = new float[TableSize];
            for (var i = 0; i < TableSize; i++)
            {
                var phase = TwoPi * i / TableSize;
                for (var h = 1; h <= harmonics; h++)
                {
                    if (oddOnly && h % 2 == 0)
                    {
                        continue;
                    }

                    table[i] += (float)Math.Sin(h * phase) / (float)Math.Pow(h, rolloff);
                }
            }

            return Normalize(table, 1f);
        }

        private static void AddTableNote(float[] buffer, int sampleRate, float[] table, float start, float length, float frequency, float amplitude, float attack, float release)
        {
            var from = (int)(start * sampleRate);
            var count = (int)(length * sampleRate);
            var step = frequency * TableSize / sampleRate;
            var phase = 0f;
            for (var n = 0; n < count && from + n < buffer.Length; n++)
            {
                var t = n / (float)sampleRate;
                var envelope = Math.Min(1f, t / attack) * Math.Min(1f, (length - t) / release);
                buffer[from + n] += amplitude * envelope * table[(int)phase];
                phase += step;
                if (phase >= TableSize) phase -= TableSize;
            }
        }

        private static void AddTableDecay(float[] buffer, int sampleRate, float[] table, float start, float length, float frequency, float amplitude, float decay)
        {
            var from = (int)(start * sampleRate);
            var count = (int)(length * sampleRate);
            var step = frequency * TableSize / sampleRate;
            var phase = 0f;
            for (var n = 0; n < count && from + n < buffer.Length; n++)
            {
                var t = n / (float)sampleRate;
                var envelope = Math.Min(1f, t / 0.004f) * (float)Math.Exp(-t / decay) * Math.Min(1f, (length - t) / 0.01f);
                buffer[from + n] += amplitude * envelope * table[(int)phase];
                phase += step;
                if (phase >= TableSize) phase -= TableSize;
            }
        }

        private static void AddSineDecay(float[] buffer, int sampleRate, float start, float length, float frequency, float amplitude, float decay)
        {
            var from = (int)(start * sampleRate);
            var count = (int)(length * sampleRate);
            for (var n = 0; n < count && from + n < buffer.Length; n++)
            {
                var t = n / (float)sampleRate;
                var envelope = Math.Min(1f, t / 0.003f) * (float)Math.Exp(-t / decay) * Math.Min(1f, (length - t) / 0.01f);
                buffer[from + n] += amplitude * envelope * (float)Math.Sin(TwoPi * frequency * t);
            }
        }

        private static void AddKick(float[] buffer, int sampleRate, float start)
        {
            var from = (int)(start * sampleRate);
            var count = (int)(0.3f * sampleRate);
            var phase = 0.0;
            for (var n = 0; n < count && from + n < buffer.Length; n++)
            {
                var t = n / (float)sampleRate;
                var frequency = 45.0 + 95.0 * Math.Exp(-t / 0.04);
                phase += TwoPi * frequency / sampleRate;
                buffer[from + n] += 0.55f * (float)(Math.Exp(-t / 0.11) * Math.Sin(phase));
            }
        }

        private static void AddSnare(float[] buffer, int sampleRate, float start, Random random)
        {
            var from = (int)(start * sampleRate);
            var count = (int)(0.22f * sampleRate);
            for (var n = 0; n < count && from + n < buffer.Length; n++)
            {
                var t = n / (float)sampleRate;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var tone = (float)Math.Sin(TwoPi * 190f * t);
                buffer[from + n] += (float)Math.Exp(-t / 0.06) * (0.22f * noise + 0.12f * tone);
            }
        }

        private static void AddHat(float[] buffer, int sampleRate, float start, Random random)
        {
            var from = (int)(start * sampleRate);
            var count = (int)(0.06f * sampleRate);
            var previous = 0f;
            for (var n = 0; n < count && from + n < buffer.Length; n++)
            {
                var t = n / (float)sampleRate;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                // Differencing white noise tilts it towards the highs.
                buffer[from + n] += 0.07f * (float)Math.Exp(-t / 0.015) * (noise - previous);
                previous = noise;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Scales <paramref name="samples"/> in place so the loudest sample is <paramref name="peak"/>.</summary>
        public static float[] Normalize(float[] samples, float peak)
        {
            var max = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                max = Math.Max(max, Math.Abs(samples[i]));
            }

            if (max <= 1e-6f)
            {
                return samples;
            }

            var gain = peak / max;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }

            return samples;
        }
    }
}
