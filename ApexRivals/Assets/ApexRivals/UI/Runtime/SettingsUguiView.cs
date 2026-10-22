using System.Collections.Generic;
using ApexRivals.Settings.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SettingsUguiView : MonoBehaviour, ISettingsView, ICancelHandler
    {
        private const string DesktopRefreshRateLabel = "Desktop";
        private static readonly string[] DesktopRefreshRateOptions = { DesktopRefreshRateLabel };

        [SerializeField] private Button displayTabButton;
        [SerializeField] private Button audioTabButton;
        [SerializeField] private GameObject displayPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private SettingsOptionSelector resolutionSelector;
        [SerializeField] private SettingsOptionSelector refreshRateSelector;
        [SerializeField] private SettingsOptionSelector fullScreenModeSelector;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button restoreDefaultsButton;
        [SerializeField] private Text statusText;

        private readonly List<SettingsResolution> _resolutions = new List<SettingsResolution>();
        private readonly List<SettingsResolution> _resolutionOptions = new List<SettingsResolution>();
        private readonly List<SettingsResolution> _refreshRateOptions = new List<SettingsResolution>();
        private readonly List<FullScreenMode> _fullScreenModeOptions = new List<FullScreenMode>
        {
            FullScreenMode.Windowed,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.ExclusiveFullScreen
        };

        private SettingsPresenter _presenter;
        private IPresentationScreenNavigator _navigator;
        private PresentationScreenState _cancelTarget = PresentationScreenState.Main;
        private SettingsResolution _selectedResolution;
        private bool _subscribed;
        private bool _rendering;
        private bool _displayOptionsRendered;
        private FullScreenMode _renderedFullScreenMode;
        private bool _renderedCanApply;

        public void Bind(SettingsPresenter presenter, IPresentationScreenNavigator navigator, PresentationScreenState cancelTarget)
        {
            _presenter = presenter;
            _navigator = navigator;
            _cancelTarget = cancelTarget;
            if (presenter == null) Unsubscribe();
            else Subscribe();
        }

        public void Render(SettingsViewModel viewModel)
        {
            _rendering = true;
            var displayChanged = DisplayOptionsChanged(viewModel);
            var navigationChanged = displayChanged || _renderedCanApply != viewModel.CanApply;
            if (displayChanged)
            {
                _resolutions.Clear();
                for (var index = 0; index < viewModel.AvailableResolutions.Count; index++)
                {
                    _resolutions.Add(viewModel.AvailableResolutions[index]);
                }

                _selectedResolution = viewModel.SelectedResolution;
                _renderedFullScreenMode = viewModel.FullScreenMode;
                RenderResolutionSelector(viewModel.SelectedResolution);
                RenderRefreshRateSelector(viewModel.SelectedResolution, viewModel.FullScreenMode);
                RenderFullScreenSelector(viewModel.FullScreenMode);
                _displayOptionsRendered = true;
            }
            SetSlider(masterVolumeSlider, viewModel.MasterVolume);
            SetSlider(musicVolumeSlider, viewModel.MusicVolume);
            SetSlider(sfxVolumeSlider, viewModel.SfxVolume);
            SetButton(applyButton, viewModel.CanApply);
            SetButton(cancelButton, true);
            SetButton(restoreDefaultsButton, true);
            UguiViewText.Set(statusText, viewModel.MessageKey == "ui.settings.display.rollbackFailed"
                ? "Display mode not saved. Apply again."
                : viewModel.MessageKey);
            if (navigationChanged) ConfigureNavigation();
            _renderedCanApply = viewModel.CanApply;
            _rendering = false;
        }

        private bool DisplayOptionsChanged(SettingsViewModel model)
        {
            if (!_displayOptionsRendered || _selectedResolution != model.SelectedResolution
                || _renderedFullScreenMode != model.FullScreenMode
                || _resolutions.Count != model.AvailableResolutions.Count)
            {
                return true;
            }

            // Compare values as well as selection: callers may supply a mutable
            // list, and a refreshed monitor catalog may contain the same modes.
            for (var index = 0; index < _resolutions.Count; index++)
            {
                if (_resolutions[index] != model.AvailableResolutions[index]) return true;
            }

            return false;
        }

        private void OnEnable()
        {
            Display.onDisplaysUpdated += OnDisplaysUpdated;
            _displayOptionsRendered = false;
            Subscribe();
            ShowDisplayTab(true);
            _presenter?.Present();
        }

        private void OnDisable()
        {
            Display.onDisplaysUpdated -= OnDisplaysUpdated;
            Unsubscribe();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && isActiveAndEnabled) _presenter?.RefreshDisplayOptions();
        }

        private void OnDisplaysUpdated() => _presenter?.RefreshDisplayOptions();

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (displayTabButton != null)
            {
                displayTabButton.onClick.AddListener(OnDisplayTabClicked);
            }

            if (audioTabButton != null)
            {
                audioTabButton.onClick.AddListener(OnAudioTabClicked);
            }

            if (resolutionSelector != null)
            {
                resolutionSelector.ValueChanged += OnResolutionChanged;
            }

            if (refreshRateSelector != null)
            {
                refreshRateSelector.ValueChanged += OnRefreshRateChanged;
            }

            if (fullScreenModeSelector != null)
            {
                fullScreenModeSelector.ValueChanged += OnFullScreenModeChanged;
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (restoreDefaultsButton != null)
            {
                restoreDefaultsButton.onClick.AddListener(OnRestoreDefaultsClicked);
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (displayTabButton != null)
            {
                displayTabButton.onClick.RemoveListener(OnDisplayTabClicked);
            }

            if (audioTabButton != null)
            {
                audioTabButton.onClick.RemoveListener(OnAudioTabClicked);
            }

            if (resolutionSelector != null)
            {
                resolutionSelector.ValueChanged -= OnResolutionChanged;
            }

            if (refreshRateSelector != null)
            {
                refreshRateSelector.ValueChanged -= OnRefreshRateChanged;
            }

            if (fullScreenModeSelector != null)
            {
                fullScreenModeSelector.ValueChanged -= OnFullScreenModeChanged;
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(OnApplyClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
            }

            if (restoreDefaultsButton != null)
            {
                restoreDefaultsButton.onClick.RemoveListener(OnRestoreDefaultsClicked);
            }

            _subscribed = false;
        }

        private void OnResolutionChanged(int index)
        {
            if (_rendering || index < 0 || index >= _resolutionOptions.Count)
            {
                return;
            }

            var option = _resolutionOptions[index];
            var selected = FindMatchingRefreshRate(option.Width, option.Height, _selectedResolution.RefreshRate);
            _presenter?.SelectResolution(selected);
        }

        private void OnRefreshRateChanged(int index)
        {
            if (_rendering || index < 0 || index >= _refreshRateOptions.Count)
            {
                return;
            }

            _presenter?.SelectResolution(_refreshRateOptions[index]);
        }

        private void OnFullScreenModeChanged(int index)
        {
            if (_rendering || index < 0 || index >= _fullScreenModeOptions.Count)
            {
                return;
            }

            _presenter?.SetFullScreenMode(_fullScreenModeOptions[index]);
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (!_rendering)
            {
                _presenter?.SetMasterVolume(value);
            }
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (!_rendering)
            {
                _presenter?.SetMusicVolume(value);
            }
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (!_rendering)
            {
                _presenter?.SetSfxVolume(value);
            }
        }

        private void OnApplyClicked()
        {
            _presenter?.Apply();
        }

        private void OnCancelClicked()
        {
            _presenter?.Cancel();
            _navigator?.Show(_cancelTarget);
        }

        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            OnCancelClicked();
        }

        private void OnRestoreDefaultsClicked()
        {
            _presenter?.RestoreDefaults();
        }

        private void OnDisplayTabClicked()
        {
            _presenter?.RefreshDisplayOptions();
            ShowDisplayTab(true);
        }

        private void OnAudioTabClicked()
        {
            ShowAudioTab(true);
        }

        private void ShowDisplayTab(bool selectFirstControl)
        {
            SetActive(displayPanel, true);
            SetActive(audioPanel, false);
            SetTabVisuals(true);
            ConfigureNavigation();

            if (selectFirstControl)
            {
                SelectIfUsable(resolutionSelector);
            }
        }

        private void ShowAudioTab(bool selectFirstControl)
        {
            SetActive(displayPanel, false);
            SetActive(audioPanel, true);
            SetTabVisuals(false);
            ConfigureNavigation();

            if (selectFirstControl)
            {
                SelectIfUsable(masterVolumeSlider);
            }
        }

        private void RenderResolutionSelector(SettingsResolution selectedResolution)
        {
            _resolutionOptions.Clear();
            var labels = new List<string>();
            var selectedIndex = 0;

            for (var index = 0; index < _resolutions.Count; index++)
            {
                var resolution = _resolutions[index];
                if (ContainsSize(_resolutionOptions, resolution.Width, resolution.Height))
                {
                    continue;
                }

                if (resolution.Width == selectedResolution.Width && resolution.Height == selectedResolution.Height)
                {
                    selectedIndex = _resolutionOptions.Count;
                }

                _resolutionOptions.Add(resolution);
                labels.Add($"{resolution.Width} x {resolution.Height}");
            }

            if (_resolutionOptions.Count == 0)
            {
                _resolutionOptions.Add(selectedResolution);
                labels.Add($"{selectedResolution.Width} x {selectedResolution.Height}");
            }

            resolutionSelector?.SetOptions(labels, selectedIndex);
        }

        private void RenderRefreshRateSelector(SettingsResolution selectedResolution, FullScreenMode fullScreenMode)
        {
            _refreshRateOptions.Clear();
            var labels = new List<string>();
            var selectedIndex = 0;

            for (var index = 0; index < _resolutions.Count; index++)
            {
                var resolution = _resolutions[index];
                if (resolution.Width != selectedResolution.Width
                    || resolution.Height != selectedResolution.Height
                    || ContainsRefreshRate(_refreshRateOptions, resolution.RefreshRate))
                {
                    continue;
                }

                if (resolution.RefreshRate == selectedResolution.RefreshRate)
                {
                    selectedIndex = _refreshRateOptions.Count;
                }

                _refreshRateOptions.Add(resolution);
                labels.Add(CreateRefreshRateLabel(resolution.RefreshRate));
            }

            if (_refreshRateOptions.Count == 0)
            {
                _refreshRateOptions.Add(selectedResolution);
                labels.Add(CreateRefreshRateLabel(selectedResolution.RefreshRate));
            }

            if (refreshRateSelector == null)
            {
                return;
            }

            if (DisplayRefreshRateRules.IsRefreshRateSelectable(fullScreenMode))
            {
                refreshRateSelector.interactable = true;
                refreshRateSelector.SetOptions(labels, selectedIndex);
                return;
            }

            var wasSelected = EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == refreshRateSelector.gameObject;

            refreshRateSelector.interactable = false;
            refreshRateSelector.SetOptions(DesktopRefreshRateOptions, 0);

            if (wasSelected)
            {
                SelectIfUsable(fullScreenModeSelector);
            }
        }

        private void RenderFullScreenSelector(FullScreenMode selectedMode)
        {
            var labels = new List<string>(_fullScreenModeOptions.Count);
            var selectedIndex = 0;
            for (var index = 0; index < _fullScreenModeOptions.Count; index++)
            {
                var mode = _fullScreenModeOptions[index];
                if (mode == selectedMode)
                {
                    selectedIndex = index;
                }

                labels.Add(CreateFullScreenModeLabel(mode));
            }

            fullScreenModeSelector?.SetOptions(labels, selectedIndex);
        }

        private SettingsResolution FindMatchingRefreshRate(int width, int height, int preferredRefreshRate)
        {
            var fallback = new SettingsResolution(width, height, 0);
            for (var index = 0; index < _resolutions.Count; index++)
            {
                var resolution = _resolutions[index];
                if (resolution.Width != width || resolution.Height != height)
                {
                    continue;
                }

                fallback = resolution;
                if (resolution.RefreshRate == preferredRefreshRate)
                {
                    return resolution;
                }
            }

            return fallback;
        }

        private void ConfigureNavigation()
        {
            var rows = new List<Selectable>(3);
            if (displayPanel != null && displayPanel.activeSelf)
            {
                AddRowIfPresent(rows, resolutionSelector);
                if (refreshRateSelector != null && refreshRateSelector.IsInteractable())
                {
                    rows.Add(refreshRateSelector);
                }

                AddRowIfPresent(rows, fullScreenModeSelector);
            }
            else
            {
                AddRowIfPresent(rows, masterVolumeSlider);
                AddRowIfPresent(rows, musicVolumeSlider);
                AddRowIfPresent(rows, sfxVolumeSlider);
            }

            var firstRow = rows.Count > 0 ? rows[0] : null;
            var lastRow = rows.Count > 0 ? rows[rows.Count - 1] : null;

            // The audio tab is optional; never link to a missing tab or navigation dead-ends.
            var lastTab = audioTabButton != null ? (Selectable)audioTabButton : displayTabButton;
            var belowTabs = firstRow != null ? firstRow : restoreDefaultsButton;

            SetNavigation(displayTabButton, applyButton, audioTabButton != null ? audioTabButton : belowTabs, null, audioTabButton);
            SetNavigation(audioTabButton, displayTabButton, belowTabs, displayTabButton, null);

            for (var index = 0; index < rows.Count; index++)
            {
                var up = index == 0 ? lastTab : rows[index - 1];
                var down = index == rows.Count - 1 ? (Selectable)restoreDefaultsButton : rows[index + 1];
                SetNavigation(rows[index], up, down, null, null);
            }

            SetNavigation(restoreDefaultsButton, lastRow != null ? lastRow : lastTab, cancelButton, null, cancelButton);
            SetNavigation(cancelButton, restoreDefaultsButton, applyButton, restoreDefaultsButton, applyButton);
            SetNavigation(applyButton, cancelButton, displayTabButton, cancelButton, null);
        }

        private static void AddRowIfPresent(List<Selectable> rows, Selectable row)
        {
            if (row != null)
            {
                rows.Add(row);
            }
        }

        private static string CreateRefreshRateLabel(int refreshRate)
        {
            return refreshRate > 0 ? $"{refreshRate} Hz" : "Default";
        }

        private static string CreateFullScreenModeLabel(FullScreenMode mode)
        {
            return mode switch
            {
                FullScreenMode.Windowed => "Windowed",
                FullScreenMode.ExclusiveFullScreen => "Exclusive Fullscreen",
                _ => "Borderless"
            };
        }

        private static bool ContainsSize(List<SettingsResolution> resolutions, int width, int height)
        {
            for (var index = 0; index < resolutions.Count; index++)
            {
                if (resolutions[index].Width == width && resolutions[index].Height == height)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsRefreshRate(List<SettingsResolution> resolutions, int refreshRate)
        {
            for (var index = 0; index < resolutions.Count; index++)
            {
                if (resolutions[index].RefreshRate == refreshRate)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = value;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void SetTabVisuals(bool displaySelected)
        {
            var displayState = displayTabButton != null ? displayTabButton.GetComponent<UguiMenuButtonVisualState>() : null;
            var audioState = audioTabButton != null ? audioTabButton.GetComponent<UguiMenuButtonVisualState>() : null;
            if (displayState != null)
            {
                displayState.IsCurrent = displaySelected;
            }

            if (audioState != null)
            {
                audioState.IsCurrent = !displaySelected;
            }
        }

        private static void SelectIfUsable(Selectable selectable)
        {
            UiFocus.Request(selectable);
        }

        private static void SetNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (selectable == null)
            {
                return;
            }

            UiNavigation.Set(selectable, up, down, left, right, wrapAround: selectable.navigation.wrapAround);
        }
    }
}
