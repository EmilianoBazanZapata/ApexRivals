using System.Collections.Generic;
using ApexRivals.Settings.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public readonly struct SettingsViewModel
    {
        public SettingsViewModel(
            IReadOnlyList<SettingsResolution> availableResolutions,
            SettingsResolution selectedResolution,
            FullScreenMode fullScreenMode,
            float masterVolume,
            float musicVolume,
            float uiVolume,
            float vehicleVolume,
            bool hasUnsavedChanges,
            bool canApply,
            PresentationFailure failure,
            string messageKey,
            float ambienceVolume = SettingsState.DefaultAmbienceVolume,
            GraphicsPreferences? graphics = null)
        {
            AvailableResolutions = availableResolutions;
            SelectedResolution = selectedResolution;
            FullScreenMode = fullScreenMode;
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            UiVolume = uiVolume;
            VehicleVolume = vehicleVolume;
            AmbienceVolume = ambienceVolume;
            Graphics = graphics ?? GraphicsPreferences.Default;
            HasUnsavedChanges = hasUnsavedChanges;
            CanApply = canApply;
            Failure = failure;
            MessageKey = messageKey;
        }

        public IReadOnlyList<SettingsResolution> AvailableResolutions { get; }
        public SettingsResolution SelectedResolution { get; }
        public FullScreenMode FullScreenMode { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float UiVolume { get; }
        public float VehicleVolume { get; }
        public float AmbienceVolume { get; }
        public GraphicsPreferences Graphics { get; }
        public bool HasUnsavedChanges { get; }
        public bool CanApply { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }
}
