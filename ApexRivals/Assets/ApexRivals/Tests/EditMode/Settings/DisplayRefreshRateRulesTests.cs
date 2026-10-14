using ApexRivals.Settings.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Settings
{
    public sealed class DisplayRefreshRateRulesTests
    {
        [TestCase(FullScreenMode.ExclusiveFullScreen, true)]
        [TestCase(FullScreenMode.FullScreenWindow, false)]
        [TestCase(FullScreenMode.Windowed, false)]
        public void RefreshRateIsSelectableOnlyInExclusiveFullscreen(FullScreenMode mode, bool expected)
        {
            Assert.That(DisplayRefreshRateRules.IsRefreshRateSelectable(mode), Is.EqualTo(expected));
        }

        [Test]
        public void RoundRefreshRateRoundsFractionalRates()
        {
            Assert.That(DisplayRefreshRateRules.RoundRefreshRate(Ratio(74973, 1000)), Is.EqualTo(75));
            Assert.That(DisplayRefreshRateRules.RoundRefreshRate(Ratio(60000, 1001)), Is.EqualTo(60));
        }

        [Test]
        public void RoundRefreshRateReturnsZeroForInvalidRatio()
        {
            Assert.That(DisplayRefreshRateRules.RoundRefreshRate(Ratio(60, 0)), Is.EqualTo(0));
        }

        [Test]
        public void ResolveExactRefreshRateReturnsDisplayRatioForRoundedValue()
        {
            var modes = new[]
            {
                Mode(1920, 1080, 60000, 1001),
                Mode(1920, 1080, 74973, 1000),
                Mode(1280, 720, 75, 1)
            };

            var result = DisplayRefreshRateRules.ResolveExactRefreshRate(modes, 1920, 1080, 75);

            Assert.That(result.numerator, Is.EqualTo(74973u));
            Assert.That(result.denominator, Is.EqualTo(1000u));
        }

        [Test]
        public void ResolveExactRefreshRateMatchesSizeBeforeRate()
        {
            var modes = new[]
            {
                Mode(1280, 720, 75, 1),
                Mode(1920, 1080, 74973, 1000)
            };

            var result = DisplayRefreshRateRules.ResolveExactRefreshRate(modes, 1920, 1080, 75);

            Assert.That(result.numerator, Is.EqualTo(74973u));
        }

        [Test]
        public void ResolveExactRefreshRateFallsBackToWholeHertzWhenNoModeMatches()
        {
            var modes = new[] { Mode(1920, 1080, 60, 1) };

            var result = DisplayRefreshRateRules.ResolveExactRefreshRate(modes, 1920, 1080, 75);

            Assert.That(result.numerator, Is.EqualTo(75u));
            Assert.That(result.denominator, Is.EqualTo(1u));
        }

        [Test]
        public void ResolveExactRefreshRateReturnsDefaultForUnsetRate()
        {
            var result = DisplayRefreshRateRules.ResolveExactRefreshRate(new Resolution[0], 1920, 1080, 0);

            Assert.That(result.numerator, Is.EqualTo(0u));
            Assert.That(result.denominator, Is.EqualTo(0u));
        }

        private static RefreshRate Ratio(uint numerator, uint denominator)
        {
            return new RefreshRate { numerator = numerator, denominator = denominator };
        }

        private static Resolution Mode(int width, int height, uint numerator, uint denominator)
        {
            return new Resolution
            {
                width = width,
                height = height,
                refreshRateRatio = Ratio(numerator, denominator)
            };
        }
    }
}
