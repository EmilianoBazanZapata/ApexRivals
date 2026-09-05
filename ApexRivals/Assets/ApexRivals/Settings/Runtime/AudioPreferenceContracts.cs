using System;

namespace ApexRivals.Settings.Runtime
{
    public readonly struct AudioPreferences
    {
        public AudioPreferences(float masterVolume, float musicVolume, float sfxVolume)
        {
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
        }

        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
    }

    public interface IAudioPreferenceSource
    {
        event Action<AudioPreferences> AudioPreferencesCommitted;

        AudioPreferences CurrentAudioPreferences { get; }
    }
}
