using System;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Sounds synthesised in code (mono PCM, -1..1) so the game ships audio without sample files: tyre skid and nitro
    /// loops, collision hits, short UI cues and a seamless music loop. Engines have their own synthesiser
    /// (<see cref="EngineSynthCore"/>). Deterministic: the same input always gives the same samples.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int MusicBeatsPerMinute = 120;
        public const int MusicBars = 8;

        private const float TwoPi = (float)(Math.PI * 2.0);
        private const int TableSize = 2048;

        // ------------------------------------------------------------------ tyres, impacts, nitro

        /// <summary>
        /// Seamless one-second tyre skid: rubber hiss (band-limited noise) under a squeal whose pitch wobbles, as the
        /// tread grips and lets go. Pitch it up a little with the slide's intensity.
        /// </summary>
        public static float[] CreateTireSkidLoop(int sampleRate)
        {
            var samples = new float[sampleRate];
            var random = new Random(23);
            var low = 0f;
            var high = 0f;
            var previousNoise = 0f;
            var phase = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                // Band-pass around 1-3 kHz: high-pass then low-pass, both one-pole.
                high = 0.94f * (high + noise - previousNoise);
                previousNoise = noise;
                low += 0.35f * (high - low);
                // Squeal: ~950 Hz with a slow wobble (4 Hz and 7 Hz, whole cycles per second).
                var frequency = 950.0 + 60.0 * Math.Sin(TwoPi * 4f * t) + 25.0 * Math.Sin(TwoPi * 7f * t);
                phase += TwoPi * frequency / sampleRate;
                var squealLevel = 0.6f + 0.4f * (float)Math.Sin(TwoPi * 3f * t);
                var squeal = (float)(Math.Sin(phase) + 0.35 * Math.Sin(2.0 * phase) + 0.15 * Math.Sin(3.0 * phase));
                samples[i] = 0.9f * low + 0.32f * squealLevel * squeal;
            }

            return Normalize(LoopCrossfade(samples, sampleRate / 10), 0.75f);
        }

        /// <summary>
        /// Collision hit: a low body thump, a crunchy noise burst and a few inharmonic metal partials ringing out.
        /// <paramref name="variant"/> changes the partials so repeated hits do not sound identical.
        /// </summary>
        public static float[] CreateImpact(int sampleRate, int variant)
        {
            var samples = new float[(int)(sampleRate * 0.6f)];
            var random = new Random(101 + variant * 17);
            var partials = new float[5];
            for (var p = 0; p < partials.Length; p++)
            {
                partials[p] = 320f + (float)random.NextDouble() * 2200f;
            }

            var low = 0f;
            var thumpPhase = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var thumpFrequency = 55.0 + 70.0 * Math.Exp(-t / 0.03);
                thumpPhase += TwoPi * thumpFrequency / sampleRate;
                var thump = (float)(Math.Exp(-t / 0.09) * Math.Sin(thumpPhase));

                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += 0.25f * (noise - low);
                var crunch = (float)Math.Exp(-t / 0.045) * (0.6f * low + 0.4f * noise);

                var metal = 0f;
                for (var p = 0; p < partials.Length; p++)
                {
                    metal += (float)(Math.Exp(-t / (0.08 + 0.05 * p)) * Math.Sin(TwoPi * partials[p] * t)) / (p + 1);
                }

                var attack = Math.Min(1f, t / 0.0015f);
                samples[i] = attack * (0.9f * thump + 0.8f * crunch + 0.35f * metal);
            }

            return Normalize(samples, 0.9f);
        }

        /// <summary>Seamless one-second nitro burn: a deep rumble under a jet-like roar of filtered noise.</summary>
        public static float[] CreateNitroLoop(int sampleRate)
        {
            var samples = new float[sampleRate];
            var random = new Random(59);
            var roar = 0f;
            var rumble = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                roar += 0.18f * (noise - roar);
                rumble += 0.02f * (noise - rumble);
                var pulse = 1f + 0.15f * (float)Math.Sin(TwoPi * 12f * t);
                samples[i] = pulse * (0.8f * roar + 2.5f * rumble) + 0.15f * (float)Math.Sin(TwoPi * 60f * t);
            }

            return Normalize(LoopCrossfade(samples, sampleRate / 8), 0.7f);
        }

        /// <summary>
        /// Makes noise-based buffers loop without a click: the last <paramref name="fade"/> samples are blended into
        /// the start and the buffer is shortened by that much, so its end runs straight into its beginning.
        /// </summary>
        private static float[] LoopCrossfade(float[] samples, int fade)
        {
            fade = Math.Max(1, Math.Min(fade, samples.Length / 2));
            var length = samples.Length - fade;
            var looped = new float[length];
            Array.Copy(samples, looped, length);
            for (var i = 0; i < fade; i++)
            {
                var mix = i / (float)fade;
                looped[i] = samples[length + i] * (1f - mix) + samples[i] * mix;
            }

            return looped;
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
