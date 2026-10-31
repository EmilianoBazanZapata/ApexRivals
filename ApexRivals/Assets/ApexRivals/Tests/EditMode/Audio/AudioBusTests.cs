using System;
using ApexRivals.Audio.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Audio
{
    public sealed class AudioBusTests
    {
        [Test]
        public void Mixer_MultipliesMasterByEachBusThroughThePerceptualCurve()
        {
            var source = new FakePreferences(new AudioPreferences(0.5f, 1f, 0.5f, 0f));
            using var mixer = new AudioBusMixer(source);

            Assert.That(mixer.GetVolume(AudioBus.Music), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(mixer.GetVolume(AudioBus.Ui), Is.EqualTo(0.0625f).Within(0.0001f));
            Assert.That(mixer.GetVolume(AudioBus.Vehicles), Is.Zero);
        }

        [Test]
        public void Mixer_FollowsLivePreviewAndStopsListeningWhenDisposed()
        {
            var source = new FakePreferences(new AudioPreferences(1f, 1f, 1f, 1f));
            var mixer = new AudioBusMixer(source);
            var changes = 0;
            mixer.Changed += () => changes++;

            source.Preview(new AudioPreferences(1f, 0.5f, 1f, 1f));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(mixer.GetVolume(AudioBus.Music), Is.EqualTo(0.25f).Within(0.0001f));

            mixer.Dispose();
            source.Preview(new AudioPreferences(1f, 0f, 1f, 1f));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(source.Listeners, Is.Zero);
        }

        [Test]
        public void Mixer_WithoutSettingsPlaysAtFullVolume()
        {
            using var mixer = new AudioBusMixer(null);

            Assert.That(mixer.GetVolume(AudioBus.Music), Is.EqualTo(1f));
            Assert.That(mixer.GetVolume(AudioBus.Vehicles), Is.EqualTo(1f));
        }

        [TestCase(0f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(0.5f, 0.25f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void ToGain_IsSilentAtZeroAndClampedAtOne(float slider, float expected)
        {
            Assert.That(AudioBusMixer.ToGain(slider), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void MusicLoop_IsEightBarsAtTheTempoAndEndsQuietlyForTheLoop()
        {
            const int rate = 22050;
            var samples = ProceduralAudio.CreateMusicLoop(rate);

            Assert.That(samples.Length, Is.EqualTo(16 * rate));
            AssertPeak(samples, 0.85f);
            Assert.That(Math.Abs(samples[samples.Length - 1]), Is.LessThan(0.05f));
        }

        [TestCase(UiSound.Move)]
        [TestCase(UiSound.Confirm)]
        [TestCase(UiSound.Back)]
        public void UiSounds_AreShortAndNotSilent(UiSound sound)
        {
            var samples = ProceduralAudio.CreateUiSound(sound, 44100);

            Assert.That(samples.Length, Is.GreaterThan(0).And.LessThan(44100 / 4));
            Assert.That(Peak(samples), Is.GreaterThan(0.3f).And.LessThanOrEqualTo(0.56f));
        }

        [Test]
        public void ProceduralAudio_IsDeterministic()
        {
            Assert.That(ProceduralAudio.CreateMusicLoop(8000), Is.EqualTo(ProceduralAudio.CreateMusicLoop(8000)));
        }

        [TestCase(0.8f, "80%")]
        [TestCase(0.004f, "0%")]
        [TestCase(1.4f, "100%")]
        public void SliderValueLabel_FormatsPercentages(float value, string expected)
        {
            Assert.That(SliderValueLabel.Format(value), Is.EqualTo(expected));
        }

        [Test]
        public void UiAudioEvents_ReachSubscribers()
        {
            UiSound? heard = null;
            void Listen(UiSound sound) => heard = sound;
            UiAudioEvents.Played += Listen;
            try
            {
                UiAudioEvents.Raise(UiSound.Back);
            }
            finally
            {
                UiAudioEvents.Played -= Listen;
            }

            Assert.That(heard, Is.EqualTo(UiSound.Back));
        }

        private static float Peak(float[] samples)
        {
            var peak = 0f;
            foreach (var sample in samples)
            {
                Assert.That(float.IsNaN(sample), Is.False);
                peak = Math.Max(peak, Math.Abs(sample));
            }

            return peak;
        }

        private static void AssertPeak(float[] samples, float expected)
        {
            Assert.That(Peak(samples), Is.EqualTo(expected).Within(0.001f));
        }

        private sealed class FakePreferences : IAudioPreferenceSource
        {
            private Action<AudioPreferences> _audible;

            public FakePreferences(AudioPreferences initial)
            {
                AudiblePreferences = initial;
                CurrentAudioPreferences = initial;
            }

            public event Action<AudioPreferences> AudioPreferencesCommitted { add { } remove { } }

            public event Action<AudioPreferences> AudiblePreferencesChanged
            {
                add { _audible += value; Listeners++; }
                remove { _audible -= value; Listeners--; }
            }

            public int Listeners { get; private set; }
            public AudioPreferences CurrentAudioPreferences { get; }
            public AudioPreferences AudiblePreferences { get; private set; }

            public void Preview(AudioPreferences preferences)
            {
                AudiblePreferences = preferences;
                _audible?.Invoke(preferences);
            }
        }
    }
}
