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
        [SerializeField] private Button displayTabButton;
        [SerializeField] private Button audioTabButton;
        [SerializeField] private GameObject displayPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private Slider resolutionSlider;
        [SerializeField] private Text resolutionValueText;
        [SerializeField] private Slider refreshRateSlider;
        [SerializeField] private Text refreshRateValueText;
        [SerializeField] private Slider fullScreenModeSlider;
        [SerializeField] private Text fullScreenModeValueText;
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

        public void Bind(SettingsPresenter presenter, IPresentationScreenNavigator navigator, PresentationScreenState cancelTarget)
        {
            _presenter = presenter;
            _navigator = navigator;
            _cancelTarget = cancelTarget;
            Subscribe();
        }

        public void Render(SettingsViewModel viewModel)
        {
            _rendering = true;
            _resolutions.Clear();
            for (var index = 0; index < viewModel.AvailableResolutions.Count; index++)
            {
                _resolutions.Add(viewModel.AvailableResolutions[index].Resolution);
            }

            _selectedResolution = viewModel.SelectedResolution;
            RenderResolutionSelector(viewModel.SelectedResolution);
            RenderRefreshRateSelector(viewModel.SelectedResolution);
            RenderFullScreenSelector(viewModel.FullScreenMode);
            SetSlider(masterVolumeSlider, viewModel.MasterVolume);
            SetSlider(musicVolumeSlider, viewModel.MusicVolume);
            SetSlider(sfxVolumeSlider, viewModel.SfxVolume);
            SetButton(applyButton, viewModel.CanApply);
            SetButton(cancelButton, true);
            SetButton(restoreDefaultsButton, true);
            UguiViewText.Set(statusText, viewModel.MessageKey);
            ConfigureNavigation();
            _rendering = false;
        }

        private void OnEnable()
        {
            Subscribe();
            ShowDisplayTab(true);
            _presenter?.Present();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

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

            if (resolutionSlider != null)
            {
                resolutionSlider.onValueChanged.AddListener(OnResolutionChanged);
            }

            if (refreshRateSlider != null)
            {
                refreshRateSlider.onValueChanged.AddListener(OnRefreshRateChanged);
            }

            if (fullScreenModeSlider != null)
            {
                fullScreenModeSlider.onValueChanged.AddListener(OnFullScreenModeChanged);
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

            if (resolutionSlider != null)
            {
                resolutionSlider.onValueChanged.RemoveListener(OnResolutionChanged);
            }

            if (refreshRateSlider != null)
            {
                refreshRateSlider.onValueChanged.RemoveListener(OnRefreshRateChanged);
            }

            if (fullScreenModeSlider != null)
            {
                fullScreenModeSlider.onValueChanged.RemoveListener(OnFullScreenModeChanged);
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

        private void OnResolutionChanged(float value)
        {
            var index = Mathf.RoundToInt(value);
            if (_rendering || index < 0 || index >= _resolutionOptions.Count)
            {
                return;
            }

            var option = _resolutionOptions[index];
            SetOptionText(resolutionValueText, _resolutionOptions, index, CreateResolutionLabel);
            var selected = FindMatchingRefreshRate(option.Width, option.Height, _selectedResolution.RefreshRate);
            _presenter?.SelectResolution(selected);
        }

        private void OnRefreshRateChanged(float value)
        {
            var index = Mathf.RoundToInt(value);
            if (_rendering || index < 0 || index >= _refreshRateOptions.Count)
            {
                return;
            }

            SetOptionText(refreshRateValueText, _refreshRateOptions, index, option => CreateRefreshRateLabel(option.RefreshRate));
            _presenter?.SelectResolution(_refreshRateOptions[index]);
        }

        private void OnFullScreenModeChanged(float value)
        {
            var index = Mathf.RoundToInt(value);
            if (_rendering || index < 0 || index >= _fullScreenModeOptions.Count)
            {
                return;
            }

            SetOptionText(fullScreenModeValueText, _fullScreenModeOptions, index, CreateFullScreenModeLabel);
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
            Debug.Log($"[UI_INPUT] Settings cancel clicked. selectedBefore={SelectedName()} cancelTarget={_cancelTarget}");
            _presenter?.Cancel();
            _navigator?.Show(_cancelTarget);
            Debug.Log($"[UI_INPUT] Settings cancel completed. selectedAfter={SelectedName()}");
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
            ConfigureNavigation();

            if (selectFirstControl)
            {
                SelectIfUsable(resolutionSlider);
            }
        }

        private void ShowAudioTab(bool selectFirstControl)
        {
            SetActive(displayPanel, false);
            SetActive(audioPanel, true);
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

            ConfigureDiscreteSlider(resolutionSlider, _resolutionOptions.Count, selectedIndex);
            SetOptionText(resolutionValueText, labels, selectedIndex);
        }

        private void RenderRefreshRateSelector(SettingsResolution selectedResolution)
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

            ConfigureDiscreteSlider(refreshRateSlider, _refreshRateOptions.Count, selectedIndex);
            SetOptionText(refreshRateValueText, labels, selectedIndex);
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

            ConfigureDiscreteSlider(fullScreenModeSlider, _fullScreenModeOptions.Count, selectedIndex);
            SetOptionText(fullScreenModeValueText, labels, selectedIndex);
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
            var firstSetting = displayPanel != null && displayPanel.activeSelf ? (Selectable)resolutionSlider : masterVolumeSlider;
            var secondSetting = displayPanel != null && displayPanel.activeSelf ? (Selectable)refreshRateSlider : musicVolumeSlider;
            var thirdSetting = displayPanel != null && displayPanel.activeSelf ? (Selectable)fullScreenModeSlider : sfxVolumeSlider;

            SetNavigation(displayTabButton, applyButton, audioTabButton, null, audioTabButton);
            SetNavigation(audioTabButton, displayTabButton, firstSetting, displayTabButton, null);
            SetNavigation(firstSetting, audioTabButton, secondSetting, null, null);
            SetNavigation(secondSetting, firstSetting, thirdSetting, null, null);
            SetNavigation(thirdSetting, secondSetting, restoreDefaultsButton, null, null);
            SetNavigation(restoreDefaultsButton, thirdSetting, cancelButton, null, cancelButton);
            SetNavigation(cancelButton, restoreDefaultsButton, applyButton, restoreDefaultsButton, applyButton);
            SetNavigation(applyButton, cancelButton, displayTabButton, cancelButton, null);
        }

        private static string CreateRefreshRateLabel(int refreshRate)
        {
            return refreshRate > 0 ? $"{refreshRate} Hz" : "Default";
        }

        private static string CreateResolutionLabel(SettingsResolution resolution)
        {
            return $"{resolution.Width} x {resolution.Height}";
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

        private static void ConfigureDiscreteSlider(Slider slider, int optionCount, int selectedIndex)
        {
            if (slider == null)
            {
                return;
            }

            var maxIndex = Mathf.Max(0, optionCount - 1);
            slider.wholeNumbers = true;
            slider.minValue = 0f;
            slider.maxValue = maxIndex;
            slider.interactable = optionCount > 0;
            slider.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, maxIndex));
        }

        private static void SetOptionText(Text text, List<string> labels, int selectedIndex)
        {
            if (text == null)
            {
                return;
            }

            var maxIndex = Mathf.Max(0, labels.Count - 1);
            var label = labels.Count > 0 ? labels[Mathf.Clamp(selectedIndex, 0, maxIndex)] : string.Empty;
            UguiViewText.Set(text, $"< {label} >");
        }

        private static void SetOptionText<T>(Text text, List<T> options, int selectedIndex, System.Func<T, string> labelFactory)
        {
            if (text == null)
            {
                return;
            }

            var maxIndex = Mathf.Max(0, options.Count - 1);
            var label = options.Count > 0 ? labelFactory(options[Mathf.Clamp(selectedIndex, 0, maxIndex)]) : string.Empty;
            UguiViewText.Set(text, $"< {label} >");
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

        private static void SelectIfUsable(Selectable selectable)
        {
            if (EventSystem.current != null && selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        private static void SetNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (selectable == null)
            {
                return;
            }

            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            selectable.navigation = navigation;
        }

        private static string SelectedName()
        {
            return EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.name
                : "null";
        }
    }
}
