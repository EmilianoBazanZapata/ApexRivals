using System;

namespace ApexRivals.Settings.Runtime
{
    public interface IAudioPreferenceSource
    {
        event Action<AudioPreferences> AudioPreferencesCommitted;

        /// <summary>
        /// Raised whenever the volumes that should be heard change: slider edits (live preview), Cancel and
        /// Restore Defaults reverting them, and Apply/Load.
        /// </summary>
        event Action<AudioPreferences> AudiblePreferencesChanged;

        AudioPreferences CurrentAudioPreferences { get; }

        /// <summary>The volumes to play right now: the draft while settings are being edited.</summary>
        AudioPreferences AudiblePreferences { get; }
    }
}
