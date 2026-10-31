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
        private IReadOnlyList<SettingsResolution> _resolutions;

        private bool _operationActive;

        public SettingsPresenter(ISettingsView view, SettingsService settings)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            CaptureDisplayOptions();
            Current = CreateViewModel(PresentationFailure.None, string.Empty);
        }

        public SettingsViewModel Current { get; private set; }

        public void Present()
        {
            // A panel open or display-change notification refreshes this owned
            // snapshot. Audio and selection edits only reuse it.
            CaptureDisplayOptions();
            Render(PresentationFailure.None, string.Empty);
        }

        public void RefreshDisplayOptions()
        {
            CaptureDisplayOptions();
            // Monitor notifications must not erase a failed Apply's recovery UI.
            Render(Current.Failure, Current.MessageKey);
        }

        private void CaptureDisplayOptions()
        {
            var available = _settings.AvailableResolutions;
            var copy = new SettingsResolution[available.Count];
            for (var index = 0; index < available.Count; index++)
            {
                copy[index] = available[index];
            }

            _resolutions = Array.AsReadOnly(copy);
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

        public void SetUiVolume(float value)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditUiVolume(value);
            Render(PresentationFailure.None, string.Empty);
        }

        public void SetVehicleVolume(float value)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditVehicleVolume(value);
            Render(PresentationFailure.None, string.Empty);
        }

        public void SetAmbienceVolume(float value)
        {
            if (_operationActive)
            {
                return;
            }

            _settings.EditAmbienceVolume(value);
            Render(PresentationFailure.None, string.Empty);
        }

        public void Cancel()
        {
            if (_operationActive)
            {
                return;
            }

            _settings.Cancel();
            Render(_settings.HasUnsavedChanges ? PresentationFailure.SettingsApplyFailed : PresentationFailure.None,
                _settings.HasUnsavedChanges ? "ui.settings.display.rollbackFailed" : "ui.settings.cancelled");
        }

        public void RestoreDefaults()
        {
            if (_operationActive)
            {
                return;
            }

            _settings.RestoreDefaults();
            CaptureDisplayOptions();
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
            CaptureDisplayOptions();

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
            return new SettingsViewModel(
                _resolutions,
                _settings.Draft.Resolution,
                _settings.Draft.FullScreenMode,
                _settings.Draft.MasterVolume,
                _settings.Draft.MusicVolume,
                _settings.Draft.UiVolume,
                _settings.Draft.VehicleVolume,
                _settings.HasUnsavedChanges,
                !_operationActive && _settings.HasUnsavedChanges,
                failure,
                messageKey ?? string.Empty,
                _settings.Draft.AmbienceVolume);
        }

        private static string ToMessageKey(SettingsOperationStatus status)
        {
            return status switch
            {
                SettingsOperationStatus.UnsupportedResolution => "ui.settings.resolution.unsupported",
                SettingsOperationStatus.DisplayApplyFailed => "ui.settings.display.failed",
                SettingsOperationStatus.StorageFailed => "ui.settings.storage.failed",
                SettingsOperationStatus.DisplayRollbackFailed => "ui.settings.display.rollbackFailed",
                _ => "ui.settings.failed"
            };
        }
    }
}
