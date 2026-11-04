using ApexRivals.DrivingAssistance.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.DrivingAssistance
{
    public sealed class DrivingLineRulesTests
    {
        [TestCase(0, 20, DrivingLineZone.Green)]
        [TestCase(12, 20, DrivingLineZone.Green)]
        [TestCase(19, 20, DrivingLineZone.Yellow)]
        [TestCase(26, 20, DrivingLineZone.Red)]
        public void ColorReflectsSpeedRelativeToTheLocalRecommendation(float speed, float limit, DrivingLineZone expected) =>
            Assert.That(DrivingLineRules.Evaluate(speed, limit), Is.EqualTo(expected));

        [Test]
        public void SameSpeedIsSafeOnAStraightButWarnsForASlowerCorner()
        {
            Assert.That(DrivingLineRules.Evaluate(25, 40), Is.EqualTo(DrivingLineZone.Green));
            Assert.That(DrivingLineRules.Evaluate(25, 26), Is.EqualTo(DrivingLineZone.Yellow));
            Assert.That(DrivingLineRules.Evaluate(25, 15), Is.EqualTo(DrivingLineZone.Red));
        }

        [Test]
        public void InvalidRecommendationNeverClaimsTheRouteIsSafe()
        {
            Assert.That(DrivingLineRules.Evaluate(20, 0), Is.EqualTo(DrivingLineZone.Red));
            Assert.That(DrivingLineRules.Evaluate(20, float.NaN), Is.EqualTo(DrivingLineZone.Red));
            Assert.That(DrivingLineRules.Evaluate(float.PositiveInfinity, 20), Is.EqualTo(DrivingLineZone.Red));
        }
    }
}
