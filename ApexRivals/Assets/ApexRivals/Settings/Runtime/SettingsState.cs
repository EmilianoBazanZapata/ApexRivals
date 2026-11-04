using System;
using UnityEngine;

namespace ApexRivals.Settings.Runtime
{
    public readonly struct SettingsState : IEquatable<SettingsState>
    {
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;
        public const float DefaultMasterVolume = 1f;
        public const float DefaultMusicVolume = 0.8f;
        public const float DefaultUiVolume = 1f;
        public const float DefaultVehicleVolume = 1f;
        public const float DefaultAmbienceVolume = 1f;

        public static readonly SettingsState Default = new SettingsState(
            DefaultWidth,
            DefaultHeight,
            FullScreenMode.FullScreenWindow,
            DefaultMasterVolume,
            DefaultMusicVolume,
            DefaultUiVolume,
            DefaultVehicleVolume,
            DefaultAmbienceVolume);

        public SettingsState(
            int width,
            int height,
            FullScreenMode fullScreenMode,
            float masterVolume,
            float musicVolume,
            float uiVolume,
            float vehicleVolume,
            float ambienceVolume = DefaultAmbienceVolume,
            GraphicsPreferences? graphics = null)
        {
            Width = width > 0 ? width : DefaultWidth;
            Height = height > 0 ? height : DefaultHeight;
            FullScreenMode = NormalizeFullScreenMode(fullScreenMode);
            MasterVolume = Clamp01(masterVolume);
            MusicVolume = Clamp01(musicVolume);
            UiVolume = Clamp01(uiVolume);
            VehicleVolume = Clamp01(vehicleVolume);
            AmbienceVolume = Clamp01(ambienceVolume);
            Graphics = graphics ?? GraphicsPreferences.Default;
        }

        public int Width { get; }
        public int Height { get; }
        public FullScreenMode FullScreenMode { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        /// <summary>Interface sounds (navigation, confirm, back).</summary>
        public float UiVolume { get; }

        /// <summary>Vehicle sounds (engines).</summary>
        public float VehicleVolume { get; }
        public float AmbienceVolume { get; }

        /// <summary>Quality level, post processing and anti-aliasing.</summary>
        public GraphicsPreferences Graphics { get; }
        public SettingsResolution Resolution => new SettingsResolution(Width, Height);
        public bool IsFullscreen => FullScreenMode != FullScreenMode.Windowed;

        public SettingsState WithResolution(SettingsResolution resolution)
        {
            return new SettingsState(
                resolution.Width,
                resolution.Height,
                FullScreenMode,
                MasterVolume,
                MusicVolume,
                UiVolume,
                VehicleVolume,
                AmbienceVolume,
                Graphics);
        }

        public SettingsState WithFullScreenMode(FullScreenMode fullScreenMode)
        {
            return new SettingsState(Width, Height, fullScreenMode, MasterVolume, MusicVolume, UiVolume, VehicleVolume, AmbienceVolume, Graphics);
        }

        public SettingsState WithMasterVolume(float value)
        {
            return new SettingsState(Width, Height, FullScreenMode, value, MusicVolume, UiVolume, VehicleVolume, AmbienceVolume, Graphics);
        }

        public SettingsState WithMusicVolume(float value)
        {
            return new SettingsState(Width, Height, FullScreenMode, MasterVolume, value, UiVolume, VehicleVolume, AmbienceVolume, Graphics);
        }

        public SettingsState WithUiVolume(float value)
        {
            return new SettingsState(Width, Height, FullScreenMode, MasterVolume, MusicVolume, value, VehicleVolume, AmbienceVolume, Graphics);
        }

        public SettingsState WithVehicleVolume(float value)
        {
            return new SettingsState(Width, Height, FullScreenMode, MasterVolume, MusicVolume, UiVolume, value, AmbienceVolume, Graphics);
        }

        public SettingsState WithAmbienceVolume(float value)
        {
            return new SettingsState(Width, Height, FullScreenMode, MasterVolume, MusicVolume, UiVolume, VehicleVolume, value, Graphics);
        }

        public SettingsState WithGraphics(GraphicsPreferences graphics)
        {
            return new SettingsState(Width, Height, FullScreenMode, MasterVolume, MusicVolume, UiVolume, VehicleVolume, AmbienceVolume, graphics);
        }

        /// <summary>True when every volume matches <paramref name="other"/>.</summary>
        public bool AudioEquals(SettingsState other)
        {
            return MasterVolume.Equals(other.MasterVolume)
                && MusicVolume.Equals(other.MusicVolume)
                && UiVolume.Equals(other.UiVolume)
                && VehicleVolume.Equals(other.VehicleVolume)
                && AmbienceVolume.Equals(other.AmbienceVolume);
        }

        public bool Equals(SettingsState other)
        {
            return Width == other.Width
                && Height == other.Height
                && FullScreenMode == other.FullScreenMode
                && MasterVolume.Equals(other.MasterVolume)
                && MusicVolume.Equals(other.MusicVolume)
                && UiVolume.Equals(other.UiVolume)
                && VehicleVolume.Equals(other.VehicleVolume)
                && AmbienceVolume.Equals(other.AmbienceVolume)
                && Graphics == other.Graphics;
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
                hash = (hash * 397) ^ (int)FullScreenMode;
                hash = (hash * 397) ^ MasterVolume.GetHashCode();
                hash = (hash * 397) ^ MusicVolume.GetHashCode();
                hash = (hash * 397) ^ UiVolume.GetHashCode();
                hash = (hash * 397) ^ VehicleVolume.GetHashCode();
                hash = (hash * 397) ^ AmbienceVolume.GetHashCode();
                hash = (hash * 397) ^ Graphics.GetHashCode();
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
