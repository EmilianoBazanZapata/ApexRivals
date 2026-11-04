using System;
using UnityEngine;

namespace ApexRivals.Settings.Runtime
{
    public sealed class PlayerPrefsSettingsStorage : ISettingsStorage
    {
        private const int CurrentVersion = 1;

        public bool SettingsExist()
        {
            return PlayerPrefs.HasKey(SettingsPreferenceKeys.SchemaVersion);
        }

        public SettingsStorageReadResult Load()
        {
            if (!SettingsExist())
            {
                return new SettingsStorageReadResult(SettingsStorageReadStatus.Missing, SettingsState.Default, string.Empty);
            }

            var rawWidth = PlayerPrefs.GetInt(SettingsPreferenceKeys.Width, SettingsState.DefaultWidth);
            var rawHeight = PlayerPrefs.GetInt(SettingsPreferenceKeys.Height, SettingsState.DefaultHeight);
            var rawFullScreenMode = PlayerPrefs.GetInt(SettingsPreferenceKeys.FullScreenMode, (int)SettingsState.Default.FullScreenMode);
            var rawMaster = PlayerPrefs.GetFloat(SettingsPreferenceKeys.MasterVolume, SettingsState.DefaultMasterVolume);
            var rawMusic = PlayerPrefs.GetFloat(SettingsPreferenceKeys.MusicVolume, SettingsState.DefaultMusicVolume);
            // Older saves had a single effects volume: it seeds both the UI and the vehicle volumes.
            var rawLegacySfx = PlayerPrefs.GetFloat(SettingsPreferenceKeys.LegacySfxVolume, SettingsState.DefaultUiVolume);
            var rawUi = PlayerPrefs.GetFloat(SettingsPreferenceKeys.UiVolume, rawLegacySfx);
            var rawVehicle = PlayerPrefs.GetFloat(SettingsPreferenceKeys.VehicleVolume, rawLegacySfx);

            var rawAmbience = PlayerPrefs.GetFloat(SettingsPreferenceKeys.AmbienceVolume, SettingsState.DefaultAmbienceVolume);
            var rawQuality = PlayerPrefs.GetInt(SettingsPreferenceKeys.GraphicsQuality, (int)GraphicsPreferences.Default.Quality);
            var rawPostProcessing = PlayerPrefs.GetInt(SettingsPreferenceKeys.PostProcessing, GraphicsPreferences.Default.PostProcessing ? 1 : 0);
            var rawAntiAliasing = PlayerPrefs.GetInt(SettingsPreferenceKeys.AntiAliasing, (int)GraphicsPreferences.Default.AntiAliasing);

            var settings = new SettingsState(
                rawWidth,
                rawHeight,
                (FullScreenMode)rawFullScreenMode,
                rawMaster,
                rawMusic,
                rawUi,
                rawVehicle,
                rawAmbience,
                new GraphicsPreferences((GraphicsQuality)rawQuality, rawPostProcessing != 0, (AntiAliasingMode)rawAntiAliasing));

            var invalid = PlayerPrefs.GetInt(SettingsPreferenceKeys.SchemaVersion, CurrentVersion) != CurrentVersion
                || rawWidth <= 0
                || rawHeight <= 0
                || !Enum.IsDefined(typeof(FullScreenMode), rawFullScreenMode)
                || float.IsNaN(rawMaster)
                || rawMaster < 0f
                || rawMaster > 1f
                || float.IsNaN(rawMusic)
                || rawMusic < 0f
                || rawMusic > 1f
                || IsInvalidVolume(rawUi)
                || IsInvalidVolume(rawVehicle)
                || IsInvalidVolume(rawAmbience)
                || !Enum.IsDefined(typeof(GraphicsQuality), rawQuality)
                || rawPostProcessing < 0 || rawPostProcessing > 1
                || !Enum.IsDefined(typeof(AntiAliasingMode), rawAntiAliasing);

            return invalid
                ? new SettingsStorageReadResult(SettingsStorageReadStatus.Invalid, settings, "Stored preferences contained unsupported values.")
                : new SettingsStorageReadResult(SettingsStorageReadStatus.Loaded, settings, string.Empty);
        }

        public SettingsStorageWriteResult Save(SettingsState settings)
        {
            PlayerPrefs.SetInt(SettingsPreferenceKeys.SchemaVersion, CurrentVersion);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.Width, settings.Width);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.Height, settings.Height);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.FullScreenMode, (int)settings.FullScreenMode);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.MasterVolume, settings.MasterVolume);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.MusicVolume, settings.MusicVolume);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.UiVolume, settings.UiVolume);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.VehicleVolume, settings.VehicleVolume);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.AmbienceVolume, settings.AmbienceVolume);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.GraphicsQuality, (int)settings.Graphics.Quality);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.PostProcessing, settings.Graphics.PostProcessing ? 1 : 0);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.AntiAliasing, (int)settings.Graphics.AntiAliasing);
            PlayerPrefs.Save();
            return new SettingsStorageWriteResult(true, string.Empty);
        }

        public SettingsStorageWriteResult RestoreDefaults(SettingsState defaults)
        {
            return Save(defaults);
        }

        private static bool IsInvalidVolume(float value)
        {
            return float.IsNaN(value) || value < 0f || value > 1f;
        }

        private static class SettingsPreferenceKeys
        {
            public const string SchemaVersion = "ApexRivals.Settings.SchemaVersion";
            public const string Width = "ApexRivals.Settings.Resolution.Width";
            public const string Height = "ApexRivals.Settings.Resolution.Height";
            public const string FullScreenMode = "ApexRivals.Settings.Display.FullScreenMode";
            public const string MasterVolume = "ApexRivals.Settings.Audio.Master";
            public const string MusicVolume = "ApexRivals.Settings.Audio.Music";
            public const string LegacySfxVolume = "ApexRivals.Settings.Audio.Sfx";
            public const string UiVolume = "ApexRivals.Settings.Audio.Ui";
            public const string VehicleVolume = "ApexRivals.Settings.Audio.Vehicles";
            public const string AmbienceVolume = "ApexRivals.Settings.Audio.Ambience";
            public const string GraphicsQuality = "ApexRivals.Settings.Graphics.Quality";
            public const string PostProcessing = "ApexRivals.Settings.Graphics.PostProcessing";
            public const string AntiAliasing = "ApexRivals.Settings.Graphics.AntiAliasing";
        }
    }
}
