using System.Collections.Generic;
using ApexRivals.Audio.Runtime;
using ApexRivals.Settings.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SettingsUguiView : MonoBehaviour, ISettingsView, ICancelHandler, IUiTabNavigation
    {
        [SerializeField, Tooltip("Disable to preserve Navigation mode, links and wrapping authored on the Selectable components. Enable to rebuild links as controls appear/disappear.")] private bool useDynamicNavigation = true;

        [SerializeField] private string refreshRateFormat = "{0} HZ";

        [Header("Display text")]
        [SerializeField] private string displayRollbackError = "DISPLAY MODE NOT SAVED. APPLY AGAIN.";
        [SerializeField] private string unsupportedResolutionError = "THIS RESOLUTION IS NOT SUPPORTED.";
        [SerializeField] private string displayError = "COULD NOT CHANGE THE DISPLAY MODE.";
        [SerializeField] private string storageError = "COULD NOT SAVE THE SETTINGS.";
        [SerializeField] private string applyError = "COULD NOT APPLY THE SETTINGS.";
        [SerializeField] private string defaultRefreshRateLabel = "DEFAULT";
        [SerializeField] private string windowedLabel = "WINDOWED";
        [SerializeField] private string exclusiveFullscreenLabel = "EXCLUSIVE FULLSCREEN";
        [SerializeField] private string borderlessLabel = "BORDERLESS";

        [SerializeField] private string desktopRefreshRateLabel = "DESKTOP";


        [SerializeField] private Button displayTabButton;
        [SerializeField] private Button audioTabButton;
        [SerializeField] private GameObject displayPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField, Tooltip("Optional CONTROLS tab: read-only keyboard and gamepad bindings.")] private Button controlsTabButton;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private SettingsOptionSelector resolutionSelector;
        [SerializeField] private SettingsOptionSelector refreshRateSelector;
        [SerializeField] private SettingsOptionSelector fullScreenModeSelector;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField, FormerlySerializedAs("sfxVolumeSlider")] private Slider uiVolumeSlider;
        [SerializeField] private Slider vehicleVolumeSlider;
        [SerializeField] private Slider ambienceVolumeSlider;
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

        private enum SettingsTab
        {
            Display,
            Audio,
            Controls
        }

        private SettingsPresenter _presenter;
        private IPresentationScreenNavigator _navigator;
        private SettingsTab _currentTab;
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
            SetSlider(uiVolumeSlider, viewModel.UiVolume);
            SetSlider(vehicleVolumeSlider, viewModel.VehicleVolume);
            SetSlider(ambienceVolumeSlider, viewModel.AmbienceVolume);
            SetButton(applyButton, viewModel.CanApply);
            SetButton(cancelButton, true);
            SetButton(restoreDefaultsButton, true);
            UguiViewText.Set(statusText, StatusLine(viewModel.MessageKey));
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

            if (controlsTabButton != null)
            {
                controlsTabButton.onClick.AddListener(OnControlsTabClicked);
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

            if (uiVolumeSlider != null)
            {
                uiVolumeSlider.onValueChanged.AddListener(OnUiVolumeChanged);
            }

            if (vehicleVolumeSlider != null)
            {
                vehicleVolumeSlider.onValueChanged.AddListener(OnVehicleVolumeChanged);
            }

            if (ambienceVolumeSlider != null)
            {
                ambienceVolumeSlider.onValueChanged.AddListener(OnAmbienceVolumeChanged);
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

            if (controlsTabButton != null)
            {
                controlsTabButton.onClick.RemoveListener(OnControlsTabClicked);
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

            if (uiVolumeSlider != null)
            {
                uiVolumeSlider.onValueChanged.RemoveListener(OnUiVolumeChanged);
            }

            if (vehicleVolumeSlider != null)
            {
                vehicleVolumeSlider.onValueChanged.RemoveListener(OnVehicleVolumeChanged);
            }

            if (ambienceVolumeSlider != null)
            {
                ambienceVolumeSlider.onValueChanged.RemoveListener(OnAmbienceVolumeChanged);
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
                _presenter?.SetMasterVolume(NormalizedSliderValue(masterVolumeSlider, value));
                UiAudioEvents.Raise(UiSound.Move);
            }
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (!_rendering)
            {
                _presenter?.SetMusicVolume(NormalizedSliderValue(musicVolumeSlider, value));
                UiAudioEvents.Raise(UiSound.Move);
            }
        }

        private void OnUiVolumeChanged(float value)
        {
            if (!_rendering)
            {
                _presenter?.SetUiVolume(NormalizedSliderValue(uiVolumeSlider, value));
                UiAudioEvents.Raise(UiSound.Move);
            }
        }

        private void OnVehicleVolumeChanged(float value)
        {
            if (!_rendering)
            {
                _presenter?.SetVehicleVolume(NormalizedSliderValue(vehicleVolumeSlider, value));
                UiAudioEvents.Raise(UiSound.Move);
            }
        }

        private void OnAmbienceVolumeChanged(float value)
        {
            if (!_rendering)
            {
                _presenter?.SetAmbienceVolume(NormalizedSliderValue(ambienceVolumeSlider, value));
                UiAudioEvents.Raise(UiSound.Move);
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
            ShowTab(SettingsTab.Audio, true);
        }

        private void OnControlsTabClicked()
        {
            ShowTab(SettingsTab.Controls, true);
        }

        // Q/E and LB/RB cycle through the tabs this screen has.
        public void ShowPreviousTab() => SwitchTab(-1);

        public void ShowNextTab() => SwitchTab(1);

        private void SwitchTab(int direction)
        {
            var tabs = AvailableTabs();
            if (!isActiveAndEnabled || tabs.Count < 2)
            {
                return;
            }

            var next = tabs[(tabs.IndexOf(_currentTab) + direction + tabs.Count) % tabs.Count];
            if (next == SettingsTab.Display)
            {
                OnDisplayTabClicked();
            }
            else
            {
                ShowTab(next, true);
            }

            UiAudioEvents.Raise(UiSound.Move);
        }

        private List<SettingsTab> AvailableTabs()
        {
            var tabs = new List<SettingsTab>(3) { SettingsTab.Display };
            if (audioTabButton != null && audioPanel != null) tabs.Add(SettingsTab.Audio);
            if (controlsTabButton != null && controlsPanel != null) tabs.Add(SettingsTab.Controls);
            return tabs;
        }

        private void ShowDisplayTab(bool selectFirstControl)
        {
            ShowTab(SettingsTab.Display, selectFirstControl);
        }

        private void ShowTab(SettingsTab tab, bool selectFirstControl)
        {
            _currentTab = tab;
            SetActive(displayPanel, tab == SettingsTab.Display);
            SetActive(audioPanel, tab == SettingsTab.Audio);
            SetActive(controlsPanel, tab == SettingsTab.Controls);
            // The controls list is read-only, so there is nothing to restore on that tab.
            if (restoreDefaultsButton != null) SetActive(restoreDefaultsButton.gameObject, tab != SettingsTab.Controls);
            SetTabVisuals(tab);
            ConfigureNavigation();

            if (!selectFirstControl)
            {
                return;
            }

            if (tab == SettingsTab.Display) SelectIfUsable(resolutionSelector);
            else if (tab == SettingsTab.Audio) SelectIfUsable(masterVolumeSlider);
            else SelectIfUsable(controlsTabButton);
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
            refreshRateSelector.SetOptions(new[] { desktopRefreshRateLabel }, 0);

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
            if (!useDynamicNavigation) return;
            var rows = new List<Selectable>(5);
            if (_currentTab == SettingsTab.Display)
            {
                AddRowIfPresent(rows, resolutionSelector);
                if (refreshRateSelector != null && refreshRateSelector.IsInteractable())
                {
                    rows.Add(refreshRateSelector);
                }

                AddRowIfPresent(rows, fullScreenModeSelector);
            }
            else if (_currentTab == SettingsTab.Audio)
            {
                AddRowIfPresent(rows, masterVolumeSlider);
                AddRowIfPresent(rows, musicVolumeSlider);
                AddRowIfPresent(rows, uiVolumeSlider);
                AddRowIfPresent(rows, vehicleVolumeSlider);
                AddRowIfPresent(rows, ambienceVolumeSlider);
            }

            var firstRow = rows.Count > 0 ? rows[0] : null;
            var lastRow = rows.Count > 0 ? rows[rows.Count - 1] : null;

            // Tabs stack in a column on the left with the open tab's rows to their right; Restore Defaults closes
            // the rows and the footer holds Back and Apply. Left/right inside a row change its value, so a row's
            // way back to the tabs is up from the first row. The audio and controls tabs are optional: never link
            // to a missing one. The controls tab has no rows and hides Restore Defaults.
            var tabs = new List<Selectable>(3);
            AddRowIfPresent(tabs, displayTabButton);
            AddRowIfPresent(tabs, audioTabButton);
            if (controlsPanel != null) AddRowIfPresent(tabs, controlsTabButton);
            var currentTab = _currentTab == SettingsTab.Audio && audioTabButton != null ? audioTabButton
                : _currentTab == SettingsTab.Controls && controlsTabButton != null ? controlsTabButton
                : (Selectable)displayTabButton;
            var lastTab = tabs.Count > 0 ? tabs[tabs.Count - 1] : null;
            var restoreVisible = restoreDefaultsButton != null && restoreDefaultsButton.gameObject.activeSelf;
            var afterRows = restoreVisible ? (Selectable)restoreDefaultsButton : applyButton;
            var rightOfTabs = firstRow != null ? firstRow : afterRows;

            for (var index = 0; index < tabs.Count; index++)
            {
                var down = index == tabs.Count - 1 ? (Selectable)cancelButton : tabs[index + 1];
                SetNavigation(tabs[index], index == 0 ? null : tabs[index - 1], down, null, rightOfTabs);
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var up = index == 0 ? currentTab : rows[index - 1];
                var down = index == rows.Count - 1 ? afterRows : rows[index + 1];
                SetNavigation(rows[index], up, down, null, null);
            }

            if (restoreVisible) SetNavigation(restoreDefaultsButton, lastRow != null ? lastRow : currentTab, applyButton, null, null);
            SetNavigation(cancelButton, lastTab, null, null, applyButton);
            SetNavigation(applyButton, restoreVisible ? restoreDefaultsButton : lastRow != null ? lastRow : currentTab, null, cancelButton, null);
        }

        /// <summary>
        /// Player-facing status line: only failures are shown. Success and info keys (applied, cancelled,
        /// defaults pending) stay silent, and raw message keys never reach the screen.
        /// </summary>
        private string StatusLine(string messageKey)
        {
            return messageKey switch
            {
                "ui.settings.display.rollbackFailed" => displayRollbackError,
                "ui.settings.resolution.unsupported" => unsupportedResolutionError,
                "ui.settings.display.failed" => displayError,
                "ui.settings.storage.failed" => storageError,
                "ui.settings.failed" => applyError,
                _ => string.Empty
            };
        }

        private static void AddRowIfPresent(List<Selectable> rows, Selectable row)
        {
            if (row != null)
            {
                rows.Add(row);
            }
        }

        private string CreateRefreshRateLabel(int refreshRate)
        {
            return refreshRate > 0 ? UguiViewText.FormatTemplate(refreshRateFormat, "{0} HZ", refreshRate) : defaultRefreshRateLabel;
        }

        private string CreateFullScreenModeLabel(FullScreenMode mode)
        {
            return mode switch
            {
                FullScreenMode.Windowed => windowedLabel,
                FullScreenMode.ExclusiveFullScreen => exclusiveFullscreenLabel,
                _ => borderlessLabel
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

        private static float NormalizedSliderValue(Slider slider, float value)
        {
            return slider != null ? Mathf.InverseLerp(slider.minValue, slider.maxValue, value) : Mathf.Clamp01(value);
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(value)));
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void SetTabVisuals(SettingsTab tab)
        {
            SetTabCurrent(displayTabButton, tab == SettingsTab.Display);
            SetTabCurrent(audioTabButton, tab == SettingsTab.Audio);
            SetTabCurrent(controlsTabButton, tab == SettingsTab.Controls);
        }

        private static void SetTabCurrent(Button tabButton, bool current)
        {
            var state = tabButton != null ? tabButton.GetComponent<UguiMenuButtonVisualState>() : null;
            if (state != null)
            {
                state.IsCurrent = current;
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
