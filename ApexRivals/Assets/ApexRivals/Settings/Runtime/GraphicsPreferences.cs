using System;

namespace ApexRivals.Settings.Runtime
{
    public enum GraphicsQuality
    {
        Low,
        Medium,
        High
    }

    public enum AntiAliasingMode
    {
        Off,
        Fxaa,
        Smaa
    }

    /// <summary>Graphics options chosen in Settings: quality level, post processing and camera anti-aliasing.</summary>
    public readonly struct GraphicsPreferences : IEquatable<GraphicsPreferences>
    {
        public static readonly GraphicsPreferences Default = new GraphicsPreferences(GraphicsQuality.High, true, AntiAliasingMode.Smaa);

        public GraphicsPreferences(GraphicsQuality quality, bool postProcessing, AntiAliasingMode antiAliasing)
        {
            Quality = Enum.IsDefined(typeof(GraphicsQuality), quality) ? quality : GraphicsQuality.High;
            PostProcessing = postProcessing;
            AntiAliasing = Enum.IsDefined(typeof(AntiAliasingMode), antiAliasing) ? antiAliasing : AntiAliasingMode.Smaa;
        }

        public GraphicsQuality Quality { get; }
        public bool PostProcessing { get; }
        public AntiAliasingMode AntiAliasing { get; }

        public GraphicsPreferences WithQuality(GraphicsQuality quality) => new GraphicsPreferences(quality, PostProcessing, AntiAliasing);
        public GraphicsPreferences WithPostProcessing(bool enabled) => new GraphicsPreferences(Quality, enabled, AntiAliasing);
        public GraphicsPreferences WithAntiAliasing(AntiAliasingMode mode) => new GraphicsPreferences(Quality, PostProcessing, mode);

        public bool Equals(GraphicsPreferences other)
        {
            return Quality == other.Quality && PostProcessing == other.PostProcessing && AntiAliasing == other.AntiAliasing;
        }

        public override bool Equals(object obj) => obj is GraphicsPreferences other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Quality * 397 ^ (PostProcessing ? 1 : 0)) * 397 ^ (int)AntiAliasing;
            }
        }

        public static bool operator ==(GraphicsPreferences left, GraphicsPreferences right) => left.Equals(right);
        public static bool operator !=(GraphicsPreferences left, GraphicsPreferences right) => !left.Equals(right);
    }
}
