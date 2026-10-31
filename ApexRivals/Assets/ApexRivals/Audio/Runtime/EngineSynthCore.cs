using System;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Physically inspired engine sound, sample by sample, for one engine (<see cref="EngineSoundProfile"/>):
    /// <list type="number">
    /// <item>The crank turns with the rpm; at each firing angle the cylinder releases a pressure pulse into its
    /// exhaust bank (stronger with throttle, a little different every time).</item>
    /// <item>Each bank's pipe is a damped waveguide (a delay with an inverting reflection), so pulses ring at the
    /// pipe's resonance; the banks meet in a collector with its own body resonance, then a muffler low-pass.</item>
    /// <item>Intake gulps, valvetrain ticks, optional turbo whine and blow-off, and exhaust pops off the throttle.</item>
    /// </list>
    /// No Unity API: it runs on the audio thread (<see cref="ProceduralEngineSynth"/>) and offline for tests.
    /// </summary>
    public sealed class EngineSynthCore
    {
        private const int MaxPulses = 24;
        private const double CycleDegrees = 720.0;
        private const float SpeedOfSound = 343f;

        private readonly int _sampleRate;
        private readonly float _dt;

        // Engine model, prepared from the profile.
        private float[] _angles = { 0f };
        private int[] _banks = { 0 };
        private float _redline = 7000f;
        private float _displacement = 5f;
        private float _roughness = 0.1f;
        private float _pipeFeedback = 0.5f;
        private float _mufflerCutoff = 1100f;
        private float _intakeLevel;
        private float _mechanicalLevel;
        private float _turboLevel;
        private float _afterfire;
        private Bank[] _bankStates = { new Bank(1) };
        private Biquad _collector;

        // Running state.
        private double _crank;
        private double _cycleBase;
        private int _nextEvent;
        private float _rpm;
        private float _throttle;
        private float _strain;
        private readonly Pulse[] _pulses = new Pulse[MaxPulses];
        private uint _seed = 0x9E3779B9u;
        private float _mufflerA;
        private float _mufflerB;
        private float _bodyLow;
        private float _intakeEnvelope;
        private float _intakeLow;
        private float _intakeBand;
        private float _mechanicalEnvelope;
        private float _previousNoise;
        private float _boost;
        private double _turboPhase;
        private float _blowOff;
        private float _previousThrottle;
        private float _dcIn;
        private float _dcOut;

        /// <param name="seed">Varies the combustion randomness, so two cars with the same engine do not sound identical.</param>
        public EngineSynthCore(int sampleRate, uint seed = 0x9E3779B9u)
        {
            _sampleRate = Math.Max(8000, sampleRate);
            _dt = 1f / _sampleRate;
            _seed = seed == 0u ? 0x9E3779B9u : seed;
        }

        /// <summary>Firing events so far; the bench lights the cylinder that fired last.</summary>
        public int FiringCount { get; private set; }

        /// <summary>
        /// How hard the engine is working to gain revs (0-1), set by the caller: harder, brighter, more saturated firing
        /// and a louder intake, so pulling through the revs sounds different from cruising at the same rpm.
        /// </summary>
        public float Strain { get; set; }

        /// <summary>Index (into the firing order) of the most recent firing event.</summary>
        public int LastFiringIndex { get; private set; }

        public void Configure(EngineSoundProfile profile)
        {
            if (profile == null || profile.FiringAngles == null || profile.FiringAngles.Length == 0)
            {
                return;
            }

            var count = profile.FiringAngles.Length;
            var angles = new float[count];
            var banks = new int[count];
            for (var i = 0; i < count; i++)
            {
                angles[i] = (float)(((profile.FiringAngles[i] % CycleDegrees) + CycleDegrees) % CycleDegrees);
                banks[i] = profile.FiringBanks != null && i < profile.FiringBanks.Length ? Math.Max(0, profile.FiringBanks[i]) : 0;
            }

            var bankCount = 1;
            foreach (var bank in banks) bankCount = Math.Max(bankCount, bank + 1);
            var states = new Bank[bankCount];
            for (var b = 0; b < bankCount; b++)
            {
                var length = profile.PipeLengths != null && profile.PipeLengths.Length > 0
                    ? profile.PipeLengths[Math.Min(b, profile.PipeLengths.Length - 1)]
                    : 0.9f;
                // A pipe open at the far end rings with a round trip of twice its length.
                states[b] = new Bank(Math.Max(1, (int)Math.Round(2f * length / SpeedOfSound * _sampleRate)));
            }

            _angles = angles;
            _banks = banks;
            _bankStates = states;
            _redline = profile.RedlineRpm;
            _displacement = profile.Displacement;
            _roughness = profile.Roughness;
            _pipeFeedback = profile.PipeFeedback;
            _mufflerCutoff = profile.MufflerCutoff;
            _intakeLevel = profile.IntakeLevel;
            _mechanicalLevel = profile.MechanicalLevel;
            _turboLevel = profile.TurboLevel;
            _afterfire = profile.Afterfire;
            _collector = Biquad.BandPass(profile.CollectorResonance, 1.4f, _sampleRate);
            _nextEvent = 0;
            _crank = 0.0;
            _cycleBase = 0.0;
            Array.Clear(_pulses, 0, _pulses.Length);
        }

        /// <summary>Renders <paramref name="count"/> mono samples at the given (target) rpm and throttle into <paramref name="output"/>.</summary>
        public void Render(float[] output, int offset, int count, float targetRpm, float targetThrottle, bool running)
        {
            var rpmBlend = 1f - (float)Math.Exp(-_dt * 25.0);
            var throttleBlend = 1f - (float)Math.Exp(-_dt * 30.0);
            var strainBlend = 1f - (float)Math.Exp(-_dt * 12.0);
            for (var n = 0; n < count; n++)
            {
                _rpm += ((running ? Math.Max(0f, targetRpm) : 0f) - _rpm) * rpmBlend;
                _throttle += (Clamp01(targetThrottle) - _throttle) * throttleBlend;
                _strain += (Clamp01(Strain) * _throttle - _strain) * strainBlend;
                output[offset + n] = running || _rpm > 50f ? NextSample() : 0f;
            }
        }

        private float NextSample()
        {
            var rpmShare = Clamp01(_rpm / Math.Max(1f, _redline));

            // Crank: advance and fire every event whose angle was passed.
            _crank += _rpm * 6.0 * _dt;
            var guard = 0;
            while (_crank >= _cycleBase + _angles[_nextEvent] && guard++ < 64)
            {
                Fire(_nextEvent, rpmShare);
                _nextEvent++;
                if (_nextEvent >= _angles.Length)
                {
                    _nextEvent = 0;
                    _cycleBase += CycleDegrees;
                }
            }

            if (_cycleBase > CycleDegrees * 100000.0)
            {
                _crank -= _cycleBase;
                _cycleBase = 0.0;
            }

            // Exhaust: pulses into their bank's pipe, banks into the collector, then the muffler.
            var tau = 0.0011f * (float)Math.Pow(_displacement / 5f, 0.35f) / (1f + 0.8f * rpmShare);
            for (var b = 0; b < _bankStates.Length; b++) _bankStates[b].Excitation = 0f;
            for (var p = 0; p < MaxPulses; p++)
            {
                if (!_pulses[p].Active) continue;
                ref var pulse = ref _pulses[p];
                var width = pulse.Pop ? tau * 2.2f : tau;
                var t = pulse.Age / width;
                var shape = t * (float)Math.Exp(1f - t) - 0.35f * (t / 3f) * (float)Math.Exp(1f - t / 3f);
                var crackle = Noise() * (float)Math.Exp(-pulse.Age / (width * (pulse.Pop ? 3f : 0.7f)));
                _bankStates[pulse.Bank].Excitation += pulse.Amplitude * (shape + (pulse.Pop ? 0.9f : 0.25f + 0.5f * _roughness) * crackle);
                pulse.Age += _dt;
                if (pulse.Age > width * (pulse.Pop ? 14f : 9f)) pulse.Active = false;
            }

            var exhaust = 0f;
            for (var b = 0; b < _bankStates.Length; b++) exhaust += _bankStates[b].Process(_pipeFeedback);
            exhaust /= (float)Math.Sqrt(_bankStates.Length);

            var body = _collector.Process(exhaust);
            var mixed = 0.55f * exhaust + 0.9f * body;
            // The muffler opens up with the throttle: brighter under load, duller when coasting.
            var cutoff = _mufflerCutoff * (0.65f + 0.7f * _throttle) * (0.8f + 0.4f * rpmShare) * (1f + 0.7f * _strain);
            var a = OnePole(cutoff);
            _mufflerA += a * (mixed - _mufflerA);
            _mufflerB += a * (_mufflerA - _mufflerB);
            _bodyLow += OnePole(110f) * (exhaust - _bodyLow);
            var exhaustOut = _mufflerB + 0.8f * _bodyLow;

            // Intake: band-limited rush, gulping with every firing, louder under load.
            _intakeEnvelope *= (float)Math.Exp(-_dt / 0.012f);
            var noise = Noise();
            var intakeCentre = 280f + 900f * rpmShare;
            var f = 2f * (float)Math.Sin(Math.PI * Math.Min(intakeCentre, _sampleRate * 0.2f) / _sampleRate);
            _intakeLow += f * _intakeBand;
            var high = noise - _intakeLow - 0.6f * _intakeBand;
            _intakeBand += f * high;
            var intake = _intakeBand * _intakeEnvelope * _intakeLevel * (0.25f + 0.75f * _throttle + 0.9f * _strain) * 0.6f;

            // Valvetrain: short high-passed ticks on each firing.
            _mechanicalEnvelope *= (float)Math.Exp(-_dt / 0.0012f);
            var ticks = (noise - _previousNoise) * _mechanicalEnvelope * _mechanicalLevel * (0.3f + 0.7f * rpmShare) * 0.35f;
            _previousNoise = noise;

            var turbo = Turbo(rpmShare);

            var sample = 0.42f * exhaustOut + intake + ticks + turbo;
            // DC blocker, then gentle saturation so loud pulses round off instead of clipping.
            var dc = sample - _dcIn + 0.995f * _dcOut;
            _dcIn = sample;
            _dcOut = dc;
            return (float)(Math.Tanh((1.3 + 0.9 * _strain) * dc) / (1.0 + 0.25 * _strain));
        }

        private void Fire(int index, float rpmShare)
        {
            FiringCount++;
            LastFiringIndex = index;
            var load = _throttle;
            var variation = 1f + _roughness * (Noise() * 0.9f);
            // Coasting still fires (weaker): the engine stays audible on the overrun.
            var amplitude = (0.32f + 0.68f * load) * (0.6f + 0.4f * rpmShare) * (1f + 0.45f * _strain) * variation
                * (float)Math.Pow(_displacement / 5f, 0.25f);
            // Off the throttle at speed, unburnt fuel lights in the hot exhaust: an occasional crackle. The chance is
            // spread over the firing rate so it means about (afterfire x 40) pops per second at any rpm.
            var firingRate = Math.Max(1f, _rpm / 120f * _angles.Length);
            var pop = load < 0.12f && rpmShare > 0.4f && Random01() < _afterfire * 40f / firingRate;
            if (pop) amplitude = 0.7f + 0.4f * Random01();

            for (var p = 0; p < MaxPulses; p++)
            {
                if (_pulses[p].Active) continue;
                _pulses[p] = new Pulse { Active = true, Bank = _banks[index], Age = 0f, Amplitude = amplitude, Pop = pop };
                break;
            }

            _intakeEnvelope += 0.6f * load + 0.15f;
            _mechanicalEnvelope = 1f;
        }

        private float Turbo(float rpmShare)
        {
            if (_turboLevel <= 0f) return 0f;

            var target = _throttle * rpmShare;
            _boost += (target - _boost) * (1f - (float)Math.Exp(-_dt * (target > _boost ? 1.6f : 3.5f)));
            // Lifting with boost up vents it: the blow-off hiss.
            if (_previousThrottle > 0.6f && _throttle < 0.25f && _boost > 0.35f) _blowOff = _boost;
            _previousThrottle = _throttle;
            _blowOff *= (float)Math.Exp(-_dt / 0.18f);

            _turboPhase += 2.0 * Math.PI * (2200.0 + 9500.0 * _boost) * _dt;
            if (_turboPhase > Math.PI * 2.0) _turboPhase -= Math.PI * 2.0;
            var whine = (float)Math.Sin(_turboPhase) * _boost * _boost * 0.12f;
            var hiss = Noise() * _blowOff * 0.35f;
            return _turboLevel * (whine + hiss);
        }

        private float OnePole(float cutoff)
        {
            return 1f - (float)Math.Exp(-2.0 * Math.PI * Math.Min(cutoff, _sampleRate * 0.45f) / _sampleRate);
        }

        private float Noise() => Random01() * 2f - 1f;

        private float Random01()
        {
            // xorshift32: cheap and allocation-free on the audio thread.
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / 16777216f;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private struct Pulse
        {
            public bool Active;
            public int Bank;
            public float Age;
            public float Amplitude;
            public bool Pop;
        }

        /// <summary>One exhaust bank: a delay line whose damped, inverted reflection makes the pipe ring.</summary>
        private sealed class Bank
        {
            private readonly float[] _line;
            private int _index;
            private float _damping;

            public Bank(int delaySamples)
            {
                _line = new float[delaySamples];
            }

            public float Excitation;

            public float Process(float feedback)
            {
                var delayed = _line[_index];
                // Losses in the pipe dull each reflection a little.
                _damping += 0.45f * (delayed - _damping);
                var output = Excitation - feedback * _damping;
                _line[_index] = output;
                _index = (_index + 1) % _line.Length;
                return output;
            }
        }

        private struct Biquad
        {
            private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

            public static Biquad BandPass(float frequency, float q, int sampleRate)
            {
                var w = 2.0 * Math.PI * Math.Min(frequency, sampleRate * 0.45) / sampleRate;
                var alpha = Math.Sin(w) / (2.0 * q);
                var a0 = 1.0 + alpha;
                return new Biquad
                {
                    _b0 = (float)(alpha / a0),
                    _b1 = 0f,
                    _b2 = (float)(-alpha / a0),
                    _a1 = (float)(-2.0 * Math.Cos(w) / a0),
                    _a2 = (float)((1.0 - alpha) / a0)
                };
            }

            public float Process(float x)
            {
                var y = _b0 * x + _z1;
                _z1 = _b1 * x - _a1 * y + _z2;
                _z2 = _b2 * x - _a2 * y;
                return y;
            }
        }
    }
}
