using System.Collections.Generic;
using ApexRivals.Settings.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SettingsUguiView : MonoBehaviour, ISettingsView
    {
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullScreenToggle;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button restoreDefaultsButton;
        [SerializeField] private Text statusText;

        private readonly List<SettingsResolution> _resolutions = new List<SettingsResolution>();
        private SettingsPresenter _presenter;
        private IPresentationScreenNavigator _navigator;
        private PresentationScreenState _cancelTarget = PresentationScreenState.Main;
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
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var options = new List<string>(viewModel.AvailableResolutions.Count);
                var selectedIndex = 0;
                for (var index = 0; index < viewModel.AvailableResolutions.Count; index++)
                {
                    var resolution = viewModel.AvailableResolutions[index].Resolution;
                    _resolutions.Add(resolution);
                    options.Add(CreateResolutionLabel(resolution));
                    if (resolution == viewModel.SelectedResolution)
                    {
                        selectedIndex = index;
                    }
                }

                resolutionDropdown.AddOptions(options);
                resolutionDropdown.value = selectedIndex;
                resolutionDropdown.RefreshShownValue();
            }

            SetToggle(fullScreenToggle, viewModel.FullScreenMode != FullScreenMode.Windowed);
            SetSlider(masterVolumeSlider, viewModel.MasterVolume);
            SetSlider(musicVolumeSlider, viewModel.MusicVolume);
            SetSlider(sfxVolumeSlider, viewModel.SfxVolume);
            SetButton(applyButton, viewModel.CanApply);
            SetButton(cancelButton, true);
            SetButton(restoreDefaultsButton, true);
            UguiViewText.Set(statusText, viewModel.MessageKey);
            _rendering = false;
        }

        private void OnEnable()
        {
            Subscribe();
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

            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            if (fullScreenToggle != null)
            {
                fullScreenToggle.onValueChanged.AddListener(OnFullScreenChanged);
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

            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            }

            if (fullScreenToggle != null)
            {
                fullScreenToggle.onValueChanged.RemoveListener(OnFullScreenChanged);
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

        private void OnResolutionChanged(int value)
        {
            if (_rendering || value < 0 || value >= _resolutions.Count)
            {
                return;
            }

            _presenter?.SelectResolution(_resolutions[value]);
        }

        private void OnFullScreenChanged(bool value)
        {
            if (!_rendering)
            {
                _presenter?.SetFullScreenMode(value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
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

        private void OnRestoreDefaultsClicked()
        {
            _presenter?.RestoreDefaults();
        }

        private static string CreateResolutionLabel(SettingsResolution resolution)
        {
            return resolution.RefreshRate > 0
                ? $"{resolution.Width} x {resolution.Height} @ {resolution.RefreshRate} Hz"
                : $"{resolution.Width} x {resolution.Height}";
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetToggle(Toggle toggle, bool value)
        {
            if (toggle != null)
            {
                toggle.isOn = value;
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
    }
}
