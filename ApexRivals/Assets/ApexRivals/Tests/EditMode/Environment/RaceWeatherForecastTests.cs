using ApexRivals.Environment.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Environment
{
    public sealed class RaceWeatherForecastTests
    {
        [TestCase(RaceWeatherPattern.Sunny, 0, 0, 0, 0)]
        [TestCase(RaceWeatherPattern.Rainy, 1, 1, 1, 1)]
        [TestCase(RaceWeatherPattern.SunnyToOvercast, 0, 0, .75f, 0)]
        [TestCase(RaceWeatherPattern.SunnyToRain, 0, 0, 1, 1)]
        [TestCase(RaceWeatherPattern.RainToSunny, 1, 1, 0, 0)]
        public void ForecastHoldsItsStartThenReachesAndKeepsItsEnd(RaceWeatherPattern pattern,
            float startClouds, float startRain, float endClouds, float endRain)
        {
            var forecast = new RaceWeatherForecast(pattern, 20, 30, 64);
            foreach (var time in new[] { 0, 10, 20 })
            {
                Assert.That(forecast.Evaluate(time).Cloudiness, Is.EqualTo(startClouds));
                Assert.That(forecast.Evaluate(time).Rain, Is.EqualTo(startRain));
            }
            foreach (var time in new[] { 50, 100, 10000 })
            {
                Assert.That(forecast.Evaluate(time).Cloudiness, Is.EqualTo(endClouds));
                Assert.That(forecast.Evaluate(time).Rain, Is.EqualTo(endRain));
            }
        }

        [Test]
        public void CloudsArriveBeforeRainAndRainStopsBeforeTheSkyClears()
        {
            var arriving = new RaceWeatherForecast(RaceWeatherPattern.SunnyToRain, 0, 100, 1).Evaluate(30);
            Assert.That(arriving.Cloudiness, Is.GreaterThan(0));
            Assert.That(arriving.Rain, Is.Zero);
            var clearing = new RaceWeatherForecast(RaceWeatherPattern.RainToSunny, 0, 100, 1).Evaluate(70);
            Assert.That(clearing.Cloudiness, Is.GreaterThan(0));
            Assert.That(clearing.Rain, Is.Zero);
            Assert.That(clearing.Wind, Is.Zero);
        }

        [Test]
        public void RandomForecastsAreReproducibleAndCoverAllPatternsWithoutDryWind()
        {
            var patterns = new System.Collections.Generic.HashSet<RaceWeatherPattern>();
            for (var seed = 0; seed < 100; seed++)
            {
                var a = RaceWeatherForecast.Random(seed);
                var b = RaceWeatherForecast.Random(seed);
                Assert.That(a.Pattern, Is.EqualTo(b.Pattern));
                Assert.That(a.Delay, Is.EqualTo(b.Delay).And.InRange(18, 40));
                Assert.That(a.Duration, Is.EqualTo(b.Duration).And.InRange(25, 45));
                patterns.Add(a.Pattern);
                for (var second = 0; second < 150; second++)
                {
                    var state = a.Evaluate(second);
                    Assert.That(state.Cloudiness, Is.InRange(0, 1));
                    Assert.That(state.Rain, Is.InRange(0, 1));
                    Assert.That(state.Wind, Is.InRange(0, state.Rain));
                    if (state.Rain == 0) Assert.That(state.Wind, Is.Zero);
                }
            }
            Assert.That(patterns.Count, Is.EqualTo(5));
        }
    }
}
