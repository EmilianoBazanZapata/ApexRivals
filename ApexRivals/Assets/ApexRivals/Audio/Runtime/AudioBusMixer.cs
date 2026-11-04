using System;
using ApexRivals.Settings.Runtime;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// The game's audio bus: master × (music | UI | vehicles | ambience), following the audible settings (including the live
    /// preview while a slider is dragged). Volumes are slider positions mapped through a perceptual curve, so half
    /// way sounds about half as loud instead of barely quieter.
    /// </summary>
    public sealed class AudioBusMixer : IDisposable
    {
        private readonly IAudioPreferenceSource _source;
        private AudioPreferences _preferences;

        public AudioBusMixer(IAudioPreferenceSource source)
        {
            _source = source;
            _preferences = source != null ? source.AudiblePreferences : new AudioPreferences(1f, 1f, 1f, 1f);
            if (_source != null)
            {
                _source.AudiblePreferencesChanged += Apply;
            }
        }

        /// <summary>Raised after any bus volume changes.</summary>
        public event Action Changed;

        public AudioPreferences Preferences => _preferences;

        /// <summary>Linear gain (0-1) for sounds routed through <paramref name="bus"/>.</summary>
        public float GetVolume(AudioBus bus)
        {
            var busSlider = bus switch
            {
                AudioBus.Music => _preferences.MusicVolume,
                AudioBus.Ui => _preferences.UiVolume,
                AudioBus.Ambience => _preferences.AmbienceVolume,
                _ => _preferences.VehicleVolume
            };

            return ToGain(_preferences.MasterVolume) * ToGain(busSlider);
        }

        public void Apply(AudioPreferences preferences)
        {
            _preferences = preferences;
            Changed?.Invoke();
        }

        /// <summary>Slider position (0-1) to linear gain: a squared curve (≈ -12 dB at half way, silent at 0).</summary>
        public static float ToGain(float slider)
        {
            if (float.IsNaN(slider) || slider <= 0f)
            {
                return 0f;
            }

            var clamped = slider >= 1f ? 1f : slider;
            return clamped * clamped;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                _source.AudiblePreferencesChanged -= Apply;
            }
        }
    }
}
