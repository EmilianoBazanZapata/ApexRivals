using System.Collections.Generic;
using ApexRivals.Settings.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public readonly struct SettingsViewModel
    {
        public SettingsViewModel(
            IReadOnlyList<SettingsResolutionViewModel> availableResolutions,
            SettingsResolution selectedResolution,
            FullScreenMode fullScreenMode,
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            bool hasUnsavedChanges,
            bool canApply,
            PresentationFailure failure,
            string messageKey)
        {
            AvailableResolutions = availableResolutions;
            SelectedResolution = selectedResolution;
            FullScreenMode = fullScreenMode;
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            HasUnsavedChanges = hasUnsavedChanges;
            CanApply = canApply;
            Failure = failure;
            MessageKey = messageKey;
        }

        public IReadOnlyList<SettingsResolutionViewModel> AvailableResolutions { get; }
        public SettingsResolution SelectedResolution { get; }
        public FullScreenMode FullScreenMode { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
        public bool HasUnsavedChanges { get; }
        public bool CanApply { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }
}
