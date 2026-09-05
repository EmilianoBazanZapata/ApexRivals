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
        public const float DefaultSfxVolume = 1f;

        public static readonly SettingsState Default = new SettingsState(
            DefaultWidth,
            DefaultHeight,
            DefaultRefreshRate,
            FullScreenMode.FullScreenWindow,
            DefaultMasterVolume,
            DefaultMusicVolume,
            DefaultSfxVolume);

        public SettingsState(
            int width,
            int height,
            int refreshRate,
            FullScreenMode fullScreenMode,
            float masterVolume,
            float musicVolume,
            float sfxVolume)
        {
            Width = width > 0 ? width : DefaultWidth;
            Height = height > 0 ? height : DefaultHeight;
            RefreshRate = Math.Max(0, refreshRate);
            FullScreenMode = NormalizeFullScreenMode(fullScreenMode);
            MasterVolume = Clamp01(masterVolume);
            MusicVolume = Clamp01(musicVolume);
            SfxVolume = Clamp01(sfxVolume);
        }

        public int Width { get; }
        public int Height { get; }
        public int RefreshRate { get; }
        public FullScreenMode FullScreenMode { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
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
                SfxVolume);
        }

        public SettingsState WithFullScreenMode(FullScreenMode fullScreenMode)
        {
            return new SettingsState(Width, Height, RefreshRate, fullScreenMode, MasterVolume, MusicVolume, SfxVolume);
        }

        public SettingsState WithMasterVolume(float value)
        {
            return new SettingsState(Width, Height, RefreshRate, FullScreenMode, value, MusicVolume, SfxVolume);
        }

        public SettingsState WithMusicVolume(float value)
        {
            return new SettingsState(Width, Height, RefreshRate, FullScreenMode, MasterVolume, value, SfxVolume);
        }

        public SettingsState WithSfxVolume(float value)
        {
            return new SettingsState(Width, Height, RefreshRate, FullScreenMode, MasterVolume, MusicVolume, value);
        }

        public bool Equals(SettingsState other)
        {
            return Width == other.Width
                && Height == other.Height
                && RefreshRate == other.RefreshRate
                && FullScreenMode == other.FullScreenMode
                && MasterVolume.Equals(other.MasterVolume)
                && MusicVolume.Equals(other.MusicVolume)
                && SfxVolume.Equals(other.SfxVolume);
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
                hash = (hash * 397) ^ SfxVolume.GetHashCode();
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
