using System.Collections.Generic;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Progression
{
    public sealed class RaceRewardTests
    {
        [TestCase(1, 500)]
        [TestCase(2, 300)]
        [TestCase(3, 200)]
        public void TryGetReward_ConfiguredPodiumPosition_ReturnsConfiguredReward(int finalPosition, int expectedReward)
        {
            var table = CreateRewardTable();

            var result = table.TryGetReward(finalPosition, out var reward);

            Assert.That(result, Is.True);
            Assert.That(reward, Is.EqualTo(expectedReward));
        }

        [Test]
        public void TryGetReward_PositionOutsidePodium_ReturnsFallbackReward()
        {
            var table = CreateRewardTable();

            var result = table.TryGetReward(4, out var reward);

            Assert.That(result, Is.True);
            Assert.That(reward, Is.EqualTo(100));
        }

        [Test]
        public void TryGetReward_InvalidPosition_DoesNotAwardCurrency()
        {
            var state = new PlayerProgressionState();
            var service = new RaceRewardService(state, CreateRewardTable());

            var result = service.TryAwardReward(new RaceResult("Player", 0, 10f), out var reward);

            Assert.That(result, Is.False);
            Assert.That(reward, Is.EqualTo(0));
            Assert.That(state.Currency, Is.EqualTo(0));
        }

        [Test]
        public void TryAwardReward_ValidRaceResult_AddsCurrency()
        {
            var state = new PlayerProgressionState();
            var service = new RaceRewardService(state, CreateRewardTable());

            var result = service.TryAwardReward(new RaceResult("Player", 2, 40f), out var reward);

            Assert.That(result, Is.True);
            Assert.That(reward, Is.EqualTo(300));
            Assert.That(state.Currency, Is.EqualTo(300));
        }

        private static RaceRewardTable CreateRewardTable()
        {
            return new RaceRewardTable(
                new Dictionary<int, int>
                {
                    [1] = 500,
                    [2] = 300,
                    [3] = 200
                },
                100);
        }
    }
}
