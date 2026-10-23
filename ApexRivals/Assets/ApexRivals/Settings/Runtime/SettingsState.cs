using System;
using UnityEngine;

namespace ApexRivals.Settings.Runtime
{
    public readonly struct SettingsState : IEquatable<SettingsState>
    {
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;
        public const int DefaultRefreshRate = 60;
        public const float DefaultMasterVolume = 1f;
        public const float DefaultMusicVolume = 0.8f;
        public const float DefaultUiVolume = 1f;
        public const float DefaultVehicleVolume = 1f;

        public static readonly SettingsState Default = new SettingsState(
            DefaultWidth,
            DefaultHeight,
            DefaultRefreshRate,
            FullScreenMode.FullScreenWindow,
            DefaultMasterVolume,
            DefaultMusicVolume,
            DefaultUiVolume,
            DefaultVehicleVolume);

        public SettingsState(
            int width,
            int height,
            int refreshRate,
            FullScreenMode fullScreenMode,
            float masterVolume,
            float musicVolume,
            float uiVolume,
            float vehicleVolume)
        {
            Width = width > 0 ? width : DefaultWidth;
            Height = height > 0 ? height : DefaultHeight;
            RefreshRate = Math.Max(0, refreshRate);
            FullScreenMode = NormalizeFullScreenMode(fullScreenMode);
            MasterVolume = Clamp01(masterVolume);
            MusicVolume = Clamp01(musicVolume);
            UiVolume = Clamp01(uiVolume);
            VehicleVolume = Clamp01(vehicleVolume);
        }

        public int Width { get; }
        public int Height { get; }
        public int RefreshRate { get; }
        public FullScreenMode FullScreenMode { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        /// <summary>Interface sounds (navigation, confirm, back).</summary>
        public float UiVolume { get; }

        /// <summary>Vehicle sounds (engines).</summary>
        public float VehicleVolume { get; }
        public SettingsResolution Resolution => new SettingsResolution(Width, Height, RefreshRate);
        public bool IsFullscreen => FullScreenMode != FullScreenMode.Windowed;

        public SettingsState WithResolution(SettingsResolution resolution)
        {
            return new SettingsState(
                resolution.Width,
                resolution.Height,
                resolution.RefreshRate,
                FullScreenMode,
                MasterVolume,
                MusicVolume,
                UiVolume,
                VehicleVolume);
        }

        public SettingsState WithFullScreenMode(FullScreenMode fullScreenMode)
        {
            return new SettingsState(Width, Height, RefreshRate, fullScreenMode, MasterVolume, MusicVolume, UiVolume, VehicleVolume);
        }

        public SettingsState WithMasterVolume(float value)
        {
            return new SettingsState(Width, Height, RefreshRate, FullScreenMode, value, MusicVolume, UiVolume, VehicleVolume);
        }

        public SettingsState WithMusicVolume(float value)
        {
            return new SettingsState(Width, Height, RefreshRate, FullScreenMode, MasterVolume, value, UiVolume, VehicleVolume);
        }

        public SettingsState WithUiVolume(float value)
        {
            return new SettingsState(Width, Height, RefreshRate, FullScreenMode, MasterVolume, MusicVolume, value, VehicleVolume);
        }

        public SettingsState WithVehicleVolume(float value)
        {
            return new SettingsState(Width, Height, RefreshRate, FullScreenMode, MasterVolume, MusicVolume, UiVolume, value);
        }

        /// <summary>True when every volume matches <paramref name="other"/>.</summary>
        public bool AudioEquals(SettingsState other)
        {
            return MasterVolume.Equals(other.MasterVolume)
                && MusicVolume.Equals(other.MusicVolume)
                && UiVolume.Equals(other.UiVolume)
                && VehicleVolume.Equals(other.VehicleVolume);
        }

        public bool Equals(SettingsState other)
        {
            return Width == other.Width
                && Height == other.Height
                && RefreshRate == other.RefreshRate
                && FullScreenMode == other.FullScreenMode
                && MasterVolume.Equals(other.MasterVolume)
                && MusicVolume.Equals(other.MusicVolume)
                && UiVolume.Equals(other.UiVolume)
                && VehicleVolume.Equals(other.VehicleVolume);
        }

        public override bool Equals(object obj)
        {
            return obj is SettingsState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Width;
                hash = (hash * 397) ^ Height;
                hash = (hash * 397) ^ RefreshRate;
                hash = (hash * 397) ^ (int)FullScreenMode;
                hash = (hash * 397) ^ MasterVolume.GetHashCode();
                hash = (hash * 397) ^ MusicVolume.GetHashCode();
                hash = (hash * 397) ^ UiVolume.GetHashCode();
                hash = (hash * 397) ^ VehicleVolume.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(SettingsState left, SettingsState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SettingsState left, SettingsState right)
        {
            return !left.Equals(right);
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value))
            {
                return 1f;
            }

            return Mathf.Clamp01(value);
        }

        private static FullScreenMode NormalizeFullScreenMode(FullScreenMode mode)
        {
            return Enum.IsDefined(typeof(FullScreenMode), mode) ? mode : FullScreenMode.FullScreenWindow;
        }
    }
}
