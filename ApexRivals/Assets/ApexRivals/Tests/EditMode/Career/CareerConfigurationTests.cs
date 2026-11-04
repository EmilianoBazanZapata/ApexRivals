using System.Collections.Generic;
using ApexRivals.Career.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Career
{
    public sealed class CareerConfigurationTests
    {
        private readonly FixtureObjects _objects = new FixtureObjects();
        private CareerTestFixture _catalogs;

        [SetUp]
        public void SetUp()
        {
            _catalogs = new CareerTestFixture(_objects);
        }

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

        [Test]
        public void ShippedShapeIsValid()
        {
            Assert.That(Validate(CareerTestFixture.CreateConfiguration()).Succeeded, Is.True, Validate(CareerTestFixture.CreateConfiguration()).Message);
        }

        [Test]
        public void MissingStarterIsRejected()
        {
            Assert.That(Validate(CareerTestFixture.CreateConfiguration(starter: "starter")).Message, Does.Contain("starter vehicle"));
        }

        [Test]
        public void DuplicateMilestoneIdIsRejected()
        {
            var milestones = new List<CareerMilestone>(CareerTestFixture.DefaultMilestones());
            milestones.Insert(0, new CareerMilestone("apex-win", "Copy", string.Empty, CareerMilestoneKind.CompleteTrack, "circuit", 0, 0));

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(milestones)).Message, Does.Contain("duplicated"));
        }

        [Test]
        public void UnknownTrackReferenceIsRejected()
        {
            var unlocks = new[] { new CareerTrackUnlock("moon", "apex-win") };

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(unlocks: unlocks)).Message, Does.Contain("unknown track"));
        }

        [Test]
        public void LockingTheInitialCircuitIsRejected()
        {
            var unlocks = new[] { new CareerTrackUnlock("circuit", "apex-win") };

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(unlocks: unlocks)).Message, Does.Contain("initial circuit"));
        }

        [TestCase(0)]
        [TestCase(-50)]
        public void NonPositivePriceIsRejected(int price)
        {
            var offers = new[] { new CareerVehicleOffer("striker", price, string.Empty) };

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(offers: offers)).Message, Does.Contain("positive price"));
        }

        [Test]
        public void SellingTheStarterIsRejected()
        {
            var offers = new[] { new CareerVehicleOffer("striker", 100, string.Empty), new CareerVehicleOffer("vanguard", 100, string.Empty) };

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(offers: offers)).Message, Does.Contain("granted"));
        }

        [Test]
        public void RaceableCarWithoutPriceIsRejected()
        {
            Assert.That(Validate(CareerTestFixture.CreateConfiguration(offers: new CareerVehicleOffer[0])).Message, Does.Contain("no career price"));
        }

        [Test]
        public void UnknownPrerequisiteIsRejected()
        {
            var offers = new[] { new CareerVehicleOffer("striker", 100, "ghost") };

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(offers: offers)).Message, Does.Contain("unknown milestone"));
        }

        [Test]
        public void UpgradeTargetAboveMaximumIsRejected()
        {
            var milestones = CareerTestFixture.DefaultMilestones();
            milestones[2] = new CareerMilestone("engine-1", "Engine", string.Empty, CareerMilestoneKind.ReachUpgradeLevel, "Engine", 4, 0);

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(milestones)).Message, Does.Contain("invalid upgrade target"));
        }

        [Test]
        public void OwningMoreCarsThanExistIsRejected()
        {
            var milestones = CareerTestFixture.DefaultMilestones();
            milestones[5] = new CareerMilestone("own-two-cars", "Own", string.Empty, CareerMilestoneKind.OwnVehicles, string.Empty, 3, 0);

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(milestones)).Message, Does.Contain("more cars"));
        }

        [Test]
        public void CircularUnlockIsReportedAsUnreachable()
        {
            // Dunes needs the Dunes win: nobody can ever race there.
            var unlocks = new[] { new CareerTrackUnlock("dunes", "dunes-win"), new CareerTrackUnlock("crossover", "dunes-win") };

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(unlocks: unlocks)).Message, Does.Contain("can never be reached"));
        }

        [Test]
        public void CarPrerequisiteThatNeedsTheCarItselfIsUnreachable()
        {
            var offers = new[] { new CareerVehicleOffer("striker", 100, "own-two-cars") };

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(offers: offers)).Message, Does.Contain("can never be reached"));
        }

        [Test]
        public void FinaleMustBeGatedByKnownMilestonesAndNotItself()
        {
            var ungated = new CareerFinale("crossover", "Final", "Win", new string[0]);
            var unknown = new CareerFinale("crossover", "Final", "Win", new[] { "ghost" });
            var self = new CareerFinale("crossover", "Final", "Win", new[] { "final-win" });

            Assert.That(Validate(CareerTestFixture.CreateConfiguration(finale: ungated)).Message, Does.Contain("gated"));
            Assert.That(Validate(CareerTestFixture.CreateConfiguration(finale: unknown)).Message, Does.Contain("unknown milestone"));
            Assert.That(Validate(CareerTestFixture.CreateConfiguration(finale: self)).Message, Does.Contain("itself"));
        }

        [Test]
        public void FinaleMilestoneMustExistOnceAndLast()
        {
            var milestones = new List<CareerMilestone>(CareerTestFixture.DefaultMilestones());
            var finale = milestones[milestones.Count - 1];
            milestones.RemoveAt(milestones.Count - 1);
            var missing = Validate(CareerTestFixture.CreateConfiguration(milestones));
            milestones.Insert(0, finale);
            var notLast = Validate(CareerTestFixture.CreateConfiguration(milestones));

            Assert.That(missing.Message, Does.Contain("exactly one final"));
            Assert.That(notLast.Message, Does.Contain("last milestone"));
        }

        private CareerValidationResult Validate(CareerConfiguration configuration)
        {
            return configuration.Validate(_catalogs.Vehicles, _catalogs.Tracks, CareerTestFixture.MaximumUpgradeLevels);
        }
    }
}
