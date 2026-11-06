using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class VehicleTelemetryPresentationTests
    {
        [Test]
        public void ConvertSpeedToKph_ConvertsMetersPerSecond()
        {
            Assert.That(VehicleTelemetryPresentation.ConvertSpeedToKph(10f), Is.EqualTo(36f).Within(0.001f));
        }

        [Test]
        public void ConvertSpeedToKph_ReturnsAbsoluteValueWhenReversing()
        {
            Assert.That(VehicleTelemetryPresentation.ConvertSpeedToKph(-5f), Is.EqualTo(18f).Within(0.001f));
        }

        [Test]
        public void FormatGear_ShowsGearNumberWhenNotReversing()
        {
            Assert.That(VehicleTelemetryPresentation.FormatGear(4, false), Is.EqualTo("4"));
        }

        [Test]
        public void FormatGear_ShowsRWhenReversingRegardlessOfGearIndex()
        {
            Assert.That(VehicleTelemetryPresentation.FormatGear(1, true), Is.EqualTo("R"));
        }

        [TestCase(6350.4f, 6350)]
        [TestCase(6350.6f, 6351)]
        public void RoundRpm_RoundsToNearestInteger(float rawRpm, int expectedRoundedRpm)
        {
            Assert.That(VehicleTelemetryPresentation.RoundRpm(rawRpm), Is.EqualTo(expectedRoundedRpm));
        }

        [Test]
        public void FormatRpm_DoesNotShowDecimalPrecision()
        {
            Assert.That(VehicleTelemetryPresentation.FormatRpm(6350), Is.EqualTo("6350"));
        }

        [TestCase(141.6f, 142)]
        [TestCase(141.4f, 141)]
        public void RoundSpeedKph_RoundsToNearestInteger(float rawSpeedKph, int expectedRoundedSpeedKph)
        {
            Assert.That(VehicleTelemetryPresentation.RoundSpeedKph(rawSpeedKph), Is.EqualTo(expectedRoundedSpeedKph));
        }

        [Test]
        public void FormatSpeedKph_AppendsUnitSuffix()
        {
            Assert.That(VehicleTelemetryPresentation.FormatSpeedKph(142), Is.EqualTo("142 km/h"));
        }
    }
}
