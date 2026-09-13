using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Settings.Runtime
{
    public sealed class SettingsService : IAudioPreferenceSource
    {
        private readonly ISettingsStorage _storage;
        private readonly IDisplaySettingsAdapter _display;
        private readonly SettingsState _defaults;

        public SettingsService(ISettingsStorage storage, IDisplaySettingsAdapter display, SettingsState? defaults = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _display = display ?? throw new ArgumentNullException(nameof(display));
            _defaults = defaults ?? SettingsState.Default;
            Current = ResolveForDisplay(_defaults);
            Draft = Current;
        }

        public event Action<AudioPreferences> AudioPreferencesCommitted;

        public SettingsState Current { get; private set; }
        public SettingsState Draft { get; private set; }
        public IReadOnlyList<SettingsResolution> AvailableResolutions => _display.SupportedResolutions;
        public bool HasUnsavedChanges => Draft != Current;
        public AudioPreferences CurrentAudioPreferences => CreateAudioPreferences(Current);

        public SettingsOperationResult Load()
        {
            if (!_storage.SettingsExist())
            {
                Current = ResolveForDisplay(_defaults);
                Draft = Current;
                return new SettingsOperationResult(SettingsOperationStatus.DefaultsLoaded, string.Empty);
            }

            var readResult = _storage.Load();
            if (!readResult.Succeeded)
            {
                Current = ResolveForDisplay(_defaults);
                Draft = Current;
                return new SettingsOperationResult(SettingsOperationStatus.LoadedDefaultsAfterFailure, readResult.Message);
            }

            Current = ResolveForDisplay(readResult.Settings);
            Draft = Current;

            return readResult.Status == SettingsStorageReadStatus.Invalid
                ? new SettingsOperationResult(SettingsOperationStatus.LoadedWithInvalidValues, readResult.Message)
                : SettingsOperationResult.Success();
        }

        public void EditResolution(SettingsResolution resolution)
        {
            Draft = Draft.WithResolution(resolution);
        }

        public void EditFullScreenMode(FullScreenMode fullScreenMode)
        {
            Draft = Draft.WithFullScreenMode(fullScreenMode);
        }

        public void EditMasterVolume(float value)
        {
            Draft = Draft.WithMasterVolume(value);
        }

        public void EditMusicVolume(float value)
        {
            Draft = Draft.WithMusicVolume(value);
        }

        public void EditSfxVolume(float value)
        {
            Draft = Draft.WithSfxVolume(value);
        }

        public SettingsOperationResult Apply()
        {
            if (!SettingsDisplayCatalog.ContainsResolution(_display.SupportedResolutions, Draft.Resolution)
                && _display.SupportedResolutions.Count > 0)
            {
                return new SettingsOperationResult(SettingsOperationStatus.UnsupportedResolution, "Selected resolution is not supported by this display.");
            }

            var displayResult = _display.Apply(Draft);
            if (!displayResult.Succeeded)
            {
                return new SettingsOperationResult(SettingsOperationStatus.DisplayApplyFailed, displayResult.Message);
            }

            var saveResult = _storage.Save(Draft);
            if (!saveResult.Succeeded)
            {
                return new SettingsOperationResult(SettingsOperationStatus.StorageFailed, saveResult.Message);
            }

            Current = Draft;
            AudioPreferencesCommitted?.Invoke(CreateAudioPreferences(Current));
            return SettingsOperationResult.Success();
        }

        public void Cancel()
        {
            Draft = Current;
        }

        public void RestoreDefaults()
        {
            Draft = ResolveForDisplay(_defaults);
        }

        public SettingsOperationResult CommitDefaults()
        {
            RestoreDefaults();
            return Apply();
        }

        private SettingsState ResolveForDisplay(SettingsState settings)
        {
            var resolution = SettingsDisplayCatalog.ResolveOrFallback(
                _display.SupportedResolutions,
                settings.Resolution,
                _display.CurrentResolution,
                _defaults.Resolution);

            return settings.WithResolution(resolution);
        }

        private static AudioPreferences CreateAudioPreferences(SettingsState settings)
        {
            return new AudioPreferences(settings.MasterVolume, settings.MusicVolume, settings.SfxVolume);
        }
    }
}
