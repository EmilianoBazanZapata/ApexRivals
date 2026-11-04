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
        private SettingsState _draft;

        public SettingsService(ISettingsStorage storage, IDisplaySettingsAdapter display, SettingsState? defaults = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _display = display ?? throw new ArgumentNullException(nameof(display));
            _defaults = defaults ?? SettingsState.Default;
            Current = ResolveForDisplay(_defaults);
            Draft = Current;
            Persisted = Current;
        }

        public event Action<AudioPreferences> AudioPreferencesCommitted;
        public event Action<AudioPreferences> AudiblePreferencesChanged;

        public SettingsState Current { get; private set; }
        public SettingsState Draft
        {
            get => _draft;
            private set
            {
                var audioChanged = !_draft.AudioEquals(value);
                _draft = value;
                if (audioChanged)
                {
                    AudiblePreferencesChanged?.Invoke(CreateAudioPreferences(_draft));
                }
            }
        }
        public SettingsState Persisted { get; private set; }
        public IReadOnlyList<SettingsResolution> AvailableResolutions => _display.SupportedResolutions;
        public bool HasUnsavedChanges => Draft != Current || Current != Persisted;
        public AudioPreferences CurrentAudioPreferences => CreateAudioPreferences(Current);
        public AudioPreferences AudiblePreferences => CreateAudioPreferences(Draft);

        public SettingsOperationResult Load()
        {
            if (!_storage.SettingsExist())
            {
                Current = ResolveForDisplay(_defaults);
                Draft = Current;
                Persisted = Current;
                return new SettingsOperationResult(SettingsOperationStatus.DefaultsLoaded, string.Empty);
            }

            var readResult = _storage.Load();
            if (!readResult.Succeeded)
            {
                Current = ResolveForDisplay(_defaults);
                Draft = Current;
                Persisted = Current;
                return new SettingsOperationResult(SettingsOperationStatus.LoadedDefaultsAfterFailure, readResult.Message);
            }

            Current = ResolveForDisplay(readResult.Settings);
            Draft = Current;
            Persisted = Current;

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

        public void EditGraphics(GraphicsPreferences graphics)
        {
            Draft = Draft.WithGraphics(graphics);
        }

        public void EditMasterVolume(float value)
        {
            Draft = Draft.WithMasterVolume(value);
        }

        public void EditMusicVolume(float value)
        {
            Draft = Draft.WithMusicVolume(value);
        }

        public void EditUiVolume(float value)
        {
            Draft = Draft.WithUiVolume(value);
        }

        public void EditVehicleVolume(float value)
        {
            Draft = Draft.WithVehicleVolume(value);
        }

        public void EditAmbienceVolume(float value)
        {
            Draft = Draft.WithAmbienceVolume(value);
        }

        public SettingsOperationResult Apply()
        {
            var resolutions = _display.SupportedResolutions;
            if (!SettingsDisplayCatalog.ContainsResolution(resolutions, Draft.Resolution)
                && resolutions.Count > 0)
            {
                return new SettingsOperationResult(SettingsOperationStatus.UnsupportedResolution, "Selected resolution is not supported by this display.");
            }

            var attempted = Draft;
            var previous = Current;
            var displayResult = TryApplyDisplay(attempted);
            if (!displayResult.Succeeded)
            {
                return new SettingsOperationResult(SettingsOperationStatus.DisplayApplyFailed, displayResult.Message);
            }

            SettingsStorageWriteResult saveResult;
            try
            {
                saveResult = _storage.Save(attempted);
            }
            catch (Exception exception)
            {
                saveResult = new SettingsStorageWriteResult(false, exception.Message);
            }

            if (!saveResult.Succeeded)
            {
                var rollback = TryApplyDisplay(previous);
                if (!rollback.Succeeded)
                {
                    // The attempted display mode is still applied. Keep that fact
                    // separate from persisted preferences; do not commit audio changes.
                    Current = previous.WithResolution(attempted.Resolution).WithFullScreenMode(attempted.FullScreenMode);
                    return new SettingsOperationResult(SettingsOperationStatus.DisplayRollbackFailed,
                        saveResult.Message + " Display rollback failed: " + rollback.Message);
                }

                return new SettingsOperationResult(SettingsOperationStatus.StorageFailed, saveResult.Message);
            }

            Current = attempted;
            Persisted = attempted;
            AudioPreferencesCommitted?.Invoke(CreateAudioPreferences(Current));
            return SettingsOperationResult.Success();
        }

        private SettingsOperationResult TryApplyDisplay(SettingsState settings)
        {
            try
            {
                return _display.Apply(settings);
            }
            catch (Exception exception)
            {
                return new SettingsOperationResult(SettingsOperationStatus.DisplayApplyFailed, exception.Message);
            }
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
            return new AudioPreferences(settings.MasterVolume, settings.MusicVolume, settings.UiVolume, settings.VehicleVolume, settings.AmbienceVolume);
        }
    }
}
