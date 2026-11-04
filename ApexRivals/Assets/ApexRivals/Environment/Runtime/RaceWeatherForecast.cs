using System;

namespace ApexRivals.Environment.Runtime
{
    public enum RaceWeatherPattern { Sunny, Rainy, SunnyToOvercast, SunnyToRain, RainToSunny }

    /// <summary>A deterministic forecast; randomness is sampled once per race, never per frame.</summary>
    public readonly struct RaceWeatherForecast
    {
        public RaceWeatherPattern Pattern { get; }
        public float Delay { get; }
        public float Duration { get; }
        public int Seed { get; }

        public RaceWeatherForecast(RaceWeatherPattern pattern, float delay, float duration, int seed)
        {
            Pattern = pattern;
            Delay = Math.Max(0, delay);
            Duration = Math.Max(0.01f, duration);
            Seed = seed;
        }

        public static RaceWeatherForecast Random(int seed)
        {
            var random = new System.Random(seed);
            return new RaceWeatherForecast((RaceWeatherPattern)random.Next(5),
                18 + (float)random.NextDouble() * 22, 25 + (float)random.NextDouble() * 20, seed);
        }

        public RaceWeatherState Evaluate(float elapsed)
        {
            var t = Clamp((elapsed - Delay) / Duration);
            var blend = Smooth(t);
            float clouds, rain;
            switch (Pattern)
            {
                case RaceWeatherPattern.Rainy: clouds = rain = 1; break;
                case RaceWeatherPattern.SunnyToOvercast: clouds = blend * 0.75f; rain = 0; break;
                case RaceWeatherPattern.SunnyToRain: clouds = blend; rain = Smooth(Clamp((t - 0.35f) / 0.65f)); break;
                case RaceWeatherPattern.RainToSunny: clouds = 1 - blend; rain = 1 - Smooth(Clamp(t / 0.65f)); break;
                default: clouds = rain = 0; break;
            }
            // Gentle gusts exist only while rain is present, and stop with the race clock.
            var gust = 0.7f + 0.3f * (float)Math.Sin(elapsed * 0.45 + (Seed % 1000) * 0.1);
            return new RaceWeatherState(clouds, rain, rain * gust);
        }

        private static float Clamp(float value) => Math.Max(0, Math.Min(1, value));
        private static float Smooth(float value) => value * value * (3 - 2 * value);
    }

    public readonly struct RaceWeatherState
    {
        public float Cloudiness { get; }
        public float Rain { get; }
        public float Wind { get; }
        public RaceWeatherState(float cloudiness, float rain, float wind)
        { Cloudiness = cloudiness; Rain = rain; Wind = wind; }
    }
}
