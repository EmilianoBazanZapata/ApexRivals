using ApexRivals.Progression.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Progression
{
    public sealed class PlayerProgressionStateTests
    {
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
