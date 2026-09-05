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
            var rawRefreshRate = PlayerPrefs.GetInt(SettingsPreferenceKeys.RefreshRate, SettingsState.DefaultRefreshRate);
            var rawFullScreenMode = PlayerPrefs.GetInt(SettingsPreferenceKeys.FullScreenMode, (int)SettingsState.Default.FullScreenMode);
            var rawMaster = PlayerPrefs.GetFloat(SettingsPreferenceKeys.MasterVolume, SettingsState.DefaultMasterVolume);
            var rawMusic = PlayerPrefs.GetFloat(SettingsPreferenceKeys.MusicVolume, SettingsState.DefaultMusicVolume);
            var rawSfx = PlayerPrefs.GetFloat(SettingsPreferenceKeys.SfxVolume, SettingsState.DefaultSfxVolume);

            var settings = new SettingsState(
                rawWidth,
                rawHeight,
                rawRefreshRate,
                (FullScreenMode)rawFullScreenMode,
                rawMaster,
                rawMusic,
                rawSfx);

            var invalid = PlayerPrefs.GetInt(SettingsPreferenceKeys.SchemaVersion, CurrentVersion) != CurrentVersion
                || rawWidth <= 0
                || rawHeight <= 0
                || rawRefreshRate < 0
                || !Enum.IsDefined(typeof(FullScreenMode), rawFullScreenMode)
                || float.IsNaN(rawMaster)
                || rawMaster < 0f
                || rawMaster > 1f
                || float.IsNaN(rawMusic)
                || rawMusic < 0f
                || rawMusic > 1f
                || float.IsNaN(rawSfx)
                || rawSfx < 0f
                || rawSfx > 1f;

            return invalid
                ? new SettingsStorageReadResult(SettingsStorageReadStatus.Invalid, settings, "Stored preferences contained unsupported values.")
                : new SettingsStorageReadResult(SettingsStorageReadStatus.Loaded, settings, string.Empty);
        }

        public SettingsStorageWriteResult Save(SettingsState settings)
        {
            PlayerPrefs.SetInt(SettingsPreferenceKeys.SchemaVersion, CurrentVersion);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.Width, settings.Width);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.Height, settings.Height);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.RefreshRate, settings.RefreshRate);
            PlayerPrefs.SetInt(SettingsPreferenceKeys.FullScreenMode, (int)settings.FullScreenMode);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.MasterVolume, settings.MasterVolume);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.MusicVolume, settings.MusicVolume);
            PlayerPrefs.SetFloat(SettingsPreferenceKeys.SfxVolume, settings.SfxVolume);
            PlayerPrefs.Save();
            return new SettingsStorageWriteResult(true, string.Empty);
        }

        public SettingsStorageWriteResult RestoreDefaults(SettingsState defaults)
        {
            return Save(defaults);
        }

        private static class SettingsPreferenceKeys
        {
            public const string SchemaVersion = "ApexRivals.Settings.SchemaVersion";
            public const string Width = "ApexRivals.Settings.Resolution.Width";
            public const string Height = "ApexRivals.Settings.Resolution.Height";
            public const string RefreshRate = "ApexRivals.Settings.Resolution.RefreshRate";
            public const string FullScreenMode = "ApexRivals.Settings.Display.FullScreenMode";
            public const string MasterVolume = "ApexRivals.Settings.Audio.Master";
            public const string MusicVolume = "ApexRivals.Settings.Audio.Music";
            public const string SfxVolume = "ApexRivals.Settings.Audio.Sfx";
        }
    }
}
