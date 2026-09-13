using System;
using System.Collections.Generic;
using ApexRivals.Settings.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public sealed class SettingsPresenter
    {
        private readonly ISettingsView _view;
        private readonly SettingsService _settings;

        private bool _operationActive;

        public SettingsPresenter(ISettingsView view, SettingsService settings)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Current = CreateViewModel(PresentationFailure.None, string.Empty);
        }

        public SettingsViewModel Current { get; private set; }

        public void Present()
        {
            Render(PresentationFailure.None, string.Empty);
        }

        public void SelectResolution(SettingsResolution resolution)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditResolution(resolution);
            Render(PresentationFailure.None, string.Empty);
        }

        public void SetFullScreenMode(FullScreenMode fullScreenMode)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditFullScreenMode(fullScreenMode);
            Render(PresentationFailure.None, string.Empty);
        }

        public void SetMasterVolume(float value)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditMasterVolume(value);
            Render(PresentationFailure.None, string.Empty);
        }

        public void SetMusicVolume(float value)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditMusicVolume(value);
            Render(PresentationFailure.None, string.Empty);
        }

        public void SetSfxVolume(float value)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditSfxVolume(value);
            Render(PresentationFailure.None, string.Empty);
        }

        public void Cancel()
        {
            if (_operationActive)
            {
                return;
            }

            _settings.Cancel();
            Render(PresentationFailure.None, "ui.settings.cancelled");
        }

        public void RestoreDefaults()
        {
            if (_operationActive)
            {
                return;
            }

            _settings.RestoreDefaults();
            Render(PresentationFailure.None, "ui.settings.defaults.pending");
        }

        public SettingsOperationResult Apply()
        {
            if (_operationActive)
            {
                return new SettingsOperationResult(SettingsOperationStatus.DisplayApplyFailed, "Settings operation is already active.");
            }

            _operationActive = true;
            Render(PresentationFailure.None, string.Empty);

            var result = _settings.Apply();
            _operationActive = false;

            if (result.Succeeded)
            {
                Render(PresentationFailure.None, "ui.settings.applied");
                return result;
            }

            Render(PresentationFailure.SettingsApplyFailed, ToMessageKey(result.Status));
            return result;
        }

        private void Render(PresentationFailure failure, string messageKey)
        {
            Current = CreateViewModel(failure, messageKey);
            _view.Render(Current);
        }

        private SettingsViewModel CreateViewModel(PresentationFailure failure, string messageKey)
        {
            var resolutions = _settings.AvailableResolutions;
            var models = new List<SettingsResolutionViewModel>(resolutions.Count);
            for (var index = 0; index < resolutions.Count; index++)
            {
                var resolution = resolutions[index];
                models.Add(new SettingsResolutionViewModel(resolution, resolution == _settings.Draft.Resolution));
            }

            return new SettingsViewModel(
                models,
                _settings.Draft.Resolution,
                _settings.Draft.FullScreenMode,
                _settings.Draft.MasterVolume,
                _settings.Draft.MusicVolume,
                _settings.Draft.SfxVolume,
                _settings.HasUnsavedChanges,
                !_operationActive && _settings.HasUnsavedChanges,
                failure,
                messageKey ?? string.Empty);
        }

        private static string ToMessageKey(SettingsOperationStatus status)
        {
            return status switch
            {
                SettingsOperationStatus.UnsupportedResolution => "ui.settings.resolution.unsupported",
                SettingsOperationStatus.DisplayApplyFailed => "ui.settings.display.failed",
                SettingsOperationStatus.StorageFailed => "ui.settings.storage.failed",
                _ => "ui.settings.failed"
            };
        }
    }
}
