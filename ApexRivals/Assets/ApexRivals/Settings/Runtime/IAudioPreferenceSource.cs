using System;

namespace ApexRivals.Settings.Runtime
{
    public interface IAudioPreferenceSource
    {
        event Action<AudioPreferences> AudioPreferencesCommitted;

        AudioPreferences CurrentAudioPreferences { get; }
    }
}
