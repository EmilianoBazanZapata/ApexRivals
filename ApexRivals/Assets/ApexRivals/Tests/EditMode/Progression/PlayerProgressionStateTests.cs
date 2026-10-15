using ApexRivals.Progression.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Progression
{
    public sealed class PlayerProgressionStateTests
    {
        [TestCase(int.MaxValue, 1)]
        [TestCase(int.MaxValue - 5, 6)]
        public void AddCurrency_OverflowIsRejectedWithoutMutationOrEvent(int balance, int amount)
        {
            var state = new PlayerProgressionState(balance);
            var events = 0;
            state.Changed += _ => events++;

            var result = state.AddCurrency(amount);

            Assert.That(result.Status, Is.EqualTo(CurrencyTransactionStatus.BalanceLimitExceeded));
            Assert.That(state.Currency, Is.EqualTo(balance));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void AddCurrency_ExactMaximumIsAccepted()
        {
            var state = new PlayerProgressionState(int.MaxValue - 5);
            var events = 0;
            state.Changed += _ => events++;

            Assert.That(state.AddCurrency(5).Succeeded, Is.True);
            Assert.That(state.Currency, Is.EqualTo(int.MaxValue));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void AddCurrency_PositiveAmount_IncreasesCurrency()
        {
            var state = new PlayerProgressionState(100);

            var result = state.AddCurrency(50);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.Currency, Is.EqualTo(150));
        }

        [Test]
        public void SpendCurrency_InsufficientCurrency_DoesNotBecomeNegative()
        {
            var state = new PlayerProgressionState(25);

            var result = state.SpendCurrency(50);

            Assert.That(result.Status, Is.EqualTo(CurrencyTransactionStatus.InsufficientCurrency));
            Assert.That(state.Currency, Is.EqualTo(25));
        }

        [Test]
        public void SpendCurrency_NegativeAmount_IsRejected()
        {
            var state = new PlayerProgressionState(25);

            var result = state.SpendCurrency(-1);

            Assert.That(result.Status, Is.EqualTo(CurrencyTransactionStatus.InvalidAmount));
            Assert.That(state.Currency, Is.EqualTo(25));
        }

        [Test]
        public void CompletedRaceCount_CannotBecomeNegative()
        {
            Assert.That(() => new PlayerProgressionState(completedRaceCount: -1), Throws.TypeOf<System.ArgumentOutOfRangeException>());

            var state = new PlayerProgressionState();

            Assert.That(() => state.SetCompletedRaceCount(-1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(state.CompletedRaceCount, Is.Zero);
        }
    }
}
