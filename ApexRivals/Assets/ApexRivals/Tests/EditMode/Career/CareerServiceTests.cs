using ApexRivals.Career.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Career
{
    public sealed class CareerServiceTests
    {
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

        [Test]
        public void FreshCareerOwnsOnlyTheGrantedStarterWithTheInitialCircuitOpen()
        {
            var career = new CareerTestFixture(_objects);

            var summary = career.Service.GetSummary();

            Assert.That(career.State.OwnedVehicleIds, Is.EqualTo(new[] { "vanguard" }));
            Assert.That(career.Progression.Currency, Is.Zero, "The starter is granted, not bought.");
            Assert.That(summary.OwnedVehicles, Is.EqualTo(1));
            Assert.That(summary.PurchasedVehicles, Is.Zero);
            Assert.That(summary.UnlockedTracks, Is.EqualTo(1));
            Assert.That(summary.TotalTracks, Is.EqualTo(3));
            Assert.That(summary.CompletionPercent, Is.Zero);
            Assert.That(summary.Objective.MilestoneId, Is.EqualTo("apex-finish"));
            Assert.That(summary.Finale.State, Is.EqualTo(CareerFinaleState.Locked));
            Assert.That(career.Service.IsTrackUnlocked("circuit"), Is.True);
            Assert.That(career.Service.IsTrackUnlocked("dunes"), Is.False);
            Assert.That(career.Service.GetTrackStatus("dunes").RequirementTitle, Is.EqualTo("Win at Apex Circuit"));
        }

        [Test]
        public void InitializeTwiceGrantsTheStarterOnce()
        {
            var career = new CareerTestFixture(_objects);

            career.Service.Initialize();

            Assert.That(career.State.OwnedVehicleIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void ValidPurchaseDeductsOnceGrantsOnceAndSaves()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);
            career.Progression.SetCurrency(2000);

            var result = career.Service.PurchaseVehicle("striker");

            Assert.That(result.Status, Is.EqualTo(CareerVehiclePurchaseStatus.Purchased));
            Assert.That(career.Service.IsVehicleOwned("striker"), Is.True);
            Assert.That(career.Save.SaveCount, Is.EqualTo(1));
            Assert.That(result.Awards, Has.Exactly(1).Matches<CareerMilestoneAward>(award => award.MilestoneId == "own-two-cars"));
            Assert.That(career.Progression.Currency, Is.EqualTo(2000 - 1800 + 300), "Price once, own-two-cars bonus once.");
            Assert.That(career.Service.GetSummary().PurchasedVehicles, Is.EqualTo(1));
        }

        [Test]
        public void DuplicatePurchaseIsIdempotent()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);
            career.Progression.SetCurrency(5000);
            career.Service.PurchaseVehicle("striker");
            var currencyAfterFirst = career.Progression.Currency;

            var second = career.Service.PurchaseVehicle("striker");

            Assert.That(second.Status, Is.EqualTo(CareerVehiclePurchaseStatus.AlreadyOwned));
            Assert.That(career.Progression.Currency, Is.EqualTo(currencyAfterFirst));
            Assert.That(career.State.OwnedVehicleIds.Count, Is.EqualTo(2));
            Assert.That(career.Save.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void InsufficientFundsLeavesStateUnchanged()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);
            career.Progression.SetCurrency(1799);

            var result = career.Service.PurchaseVehicle("striker");

            Assert.That(result.Status, Is.EqualTo(CareerVehiclePurchaseStatus.InsufficientFunds));
            Assert.That(career.Progression.Currency, Is.EqualTo(1799));
            Assert.That(career.Service.IsVehicleOwned("striker"), Is.False);
            Assert.That(career.Save.SaveCount, Is.Zero);
        }

        [Test]
        public void UnmetPrerequisiteLeavesStateUnchanged()
        {
            var career = new CareerTestFixture(_objects);
            career.Progression.SetCurrency(5000);

            var result = career.Service.PurchaseVehicle("striker");

            Assert.That(result.Status, Is.EqualTo(CareerVehiclePurchaseStatus.PrerequisiteNotMet));
            Assert.That(result.Message, Does.Contain("Win at Apex Circuit"));
            Assert.That(career.Progression.Currency, Is.EqualTo(5000));
            Assert.That(career.Service.IsVehicleOwned("striker"), Is.False);
        }

        [TestCase("missing", CareerVehiclePurchaseStatus.UnknownVehicle)]
        [TestCase("", CareerVehiclePurchaseStatus.UnknownVehicle)]
        [TestCase("starter", CareerVehiclePurchaseStatus.NotForSale)]
        [TestCase("vanguard", CareerVehiclePurchaseStatus.AlreadyOwned)]
        public void InvalidPurchaseRequestsChangeNothing(string vehicleId, CareerVehiclePurchaseStatus expected)
        {
            var career = new CareerTestFixture(_objects);
            career.Progression.SetCurrency(5000);

            var result = career.Service.PurchaseVehicle(vehicleId);

            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(career.Progression.Currency, Is.EqualTo(5000));
            Assert.That(career.State.OwnedVehicleIds, Is.EqualTo(new[] { "vanguard" }));
        }

        [Test]
        public void PurchaseSaveFailureRollsBackCurrencyOwnershipAndMilestones()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);
            career.Progression.SetCurrency(2000);
            career.Save.ShouldFail = true;

            var result = career.Service.PurchaseVehicle("striker");

            Assert.That(result.Status, Is.EqualTo(CareerVehiclePurchaseStatus.SaveFailed));
            Assert.That(result.Message, Is.EqualTo("Disk full."));
            Assert.That(career.Progression.Currency, Is.EqualTo(2000));
            Assert.That(career.Service.IsVehicleOwned("striker"), Is.False);
            Assert.That(career.State.HasAchieved("own-two-cars"), Is.False);
        }

        [Test]
        public void SelectionServiceRejectsCommittingAnUnownedCar()
        {
            var career = new CareerTestFixture(_objects);
            var selection = new VehicleSelectionService(career.Vehicles, career.Selected, ownership: career.Service);

            var preview = selection.Select("striker");
            var confirm = selection.ConfirmSelection();

            Assert.That(preview.Succeeded, Is.True, "Locked cars can still be browsed.");
            Assert.That(confirm.Status, Is.EqualTo(VehicleSelectionStatus.VehicleNotOwned));
            Assert.That(career.Selected.SelectedVehicleId, Is.EqualTo("vanguard"));
            Assert.That(selection.GetState().Vehicles, Has.Exactly(1).Matches<VehicleSelectionItemState>(item => item.VehicleId == "striker" && !item.Owned));
        }

        [Test]
        public void PersistedSelectionOfAnUnownedCarFallsBackToTheStarter()
        {
            var career = new CareerTestFixture(_objects);
            career.Selected.Restore("striker", true);
            var selection = new VehicleSelectionService(career.Vehicles, career.Selected, ownership: career.Service);

            selection.EnsurePersistedSelectionIsValid();

            Assert.That(career.Selected.HasCommittedVehicleSelection, Is.False);
            Assert.That(selection.CurrentCandidateVehicleId, Is.EqualTo("vanguard"));
        }

        [Test]
        public void RaceStartIsRejectedOnALockedCircuit()
        {
            var career = new CareerTestFixture(_objects);

            var request = career.Service.PrepareRace("dunes", false);
            var directStart = career.Service.CheckRaceStart("RaceDunes");

            Assert.That(request.Status, Is.EqualTo(CareerRaceRequestStatus.TrackLocked));
            Assert.That(request.Message, Does.Contain("Win at Apex Circuit"));
            Assert.That(directStart.Allowed, Is.False, "Scene flow cannot bypass the unlock.");
        }

        [Test]
        public void RaceStartIsRejectedWithAnUnownedCar()
        {
            var career = new CareerTestFixture(_objects);
            career.Selected.Restore("striker", true);

            Assert.That(career.Service.PrepareRace("circuit", false).Status, Is.EqualTo(CareerRaceRequestStatus.VehicleNotOwned));
            Assert.That(career.Service.CheckRaceStart("Race").Allowed, Is.False);
        }

        [Test]
        public void WinningTheInitialCircuitUnlocksTheNextAndReportsIt()
        {
            var career = new CareerTestFixture(_objects);

            var outcome = career.Race("circuit", 1);

            Assert.That(outcome.Status, Is.EqualTo(CareerRaceOutcomeStatus.Recorded));
            Assert.That(outcome.NewlyUnlockedTracks, Is.EqualTo(new[] { "Dunes Circuit" }));
            Assert.That(outcome.NewlyPurchasableVehicles, Is.EqualTo(new[] { "striker" }));
            Assert.That(outcome.BonusCurrency, Is.EqualTo(500));
            Assert.That(career.Service.IsTrackUnlocked("dunes"), Is.True);
            Assert.That(outcome.Summary.Objective.MilestoneId, Is.EqualTo("engine-1"));
        }

        [Test]
        public void ResultsAreAttributedToTheCircuitActuallyLoaded()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);
            career.Service.PrepareRace("circuit", false);
            career.Service.CheckRaceStart("Race");

            var attempt = career.Service.BeginAttempt("RaceDunes", "vanguard");
            career.Service.RecordRaceFinish(attempt, new RaceResult(CareerTestFixture.Player, 1, 50f), CareerTestFixture.Player);

            Assert.That(attempt.TrackId, Is.EqualTo("dunes"));
            Assert.That(career.State.HasWonTrack("dunes"), Is.True);
            Assert.That(career.State.CompletedTrackIds, Is.EqualTo(new[] { "circuit", "dunes" }));
        }

        [Test]
        public void ReplaysNeverPayAMilestoneTwiceOrRaiseCompletion()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);
            var completion = career.Service.GetSummary().CompletionPercent;
            var currency = career.Progression.Currency;

            var replay = career.Race("circuit", 1);

            Assert.That(replay.Awards, Is.Empty);
            Assert.That(career.Progression.Currency, Is.EqualTo(currency), "Race prize money is paid by the reward service, not the career.");
            Assert.That(career.Service.GetSummary().CompletionPercent, Is.EqualTo(completion));
        }

        [Test]
        public void SpendingNeverLowersCompletion()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);
            career.Progression.SetCurrency(1800);
            var before = career.Service.GetSummary().CompletionPercent;

            career.Service.PurchaseVehicle("striker");
            career.Progression.SpendCurrency(career.Progression.Currency);

            Assert.That(career.Service.GetSummary().CompletionPercent, Is.GreaterThanOrEqualTo(before));
        }

        [TestCase(0, 9, false, 0)]
        [TestCase(1, 9, false, 11)]
        [TestCase(8, 9, false, 88)]
        [TestCase(9, 9, false, 99)]
        [TestCase(9, 9, true, 100)]
        [TestCase(12, 9, true, 100)]
        [TestCase(-3, 9, false, 0)]
        [TestCase(3, 0, false, 0)]
        public void CompletionPercentIsFlooredClampedAndHeldBelow100UntilTheFinal(int achieved, int total, bool finaleWon, int expected)
        {
            Assert.That(CareerService.ComputeCompletionPercent(achieved, total, finaleWon), Is.EqualTo(expected));
        }

        [Test]
        public void SameAttemptIsProcessedOnlyOnce()
        {
            var career = new CareerTestFixture(_objects);
            var attempt = career.Start("circuit");
            var result = new RaceResult(CareerTestFixture.Player, 1, 50f);

            var first = career.Service.RecordRaceFinish(attempt, result, CareerTestFixture.Player);
            var currency = career.Progression.Currency;
            var second = career.Service.RecordRaceFinish(attempt, result, CareerTestFixture.Player);

            Assert.That(first.Status, Is.EqualTo(CareerRaceOutcomeStatus.Recorded));
            Assert.That(second.Status, Is.EqualTo(CareerRaceOutcomeStatus.AlreadyProcessed));
            Assert.That(second.Awards, Is.Empty);
            Assert.That(career.Progression.Currency, Is.EqualTo(currency));
        }

        [Test]
        public void OpponentAndInvalidFinishesNeverCountForThePlayer()
        {
            var career = new CareerTestFixture(_objects);
            var attempt = career.Start("circuit");

            var opponent = career.Service.RecordRaceFinish(attempt, new RaceResult("AI_01", 1, 40f), CareerTestFixture.Player);
            var invalid = career.Service.RecordRaceFinish(attempt, new RaceResult(CareerTestFixture.Player, 0, 40f), CareerTestFixture.Player);

            Assert.That(opponent.Status, Is.EqualTo(CareerRaceOutcomeStatus.InvalidResult));
            Assert.That(invalid.Status, Is.EqualTo(CareerRaceOutcomeStatus.InvalidResult));
            Assert.That(career.State.CompletedTrackIds, Is.Empty);
            Assert.That(career.State.AchievedMilestoneIds, Is.Empty);
        }

        [Test]
        public void AbandonedAttemptsLeaveNoTraceAndRetriesGetANewIdentity()
        {
            var career = new CareerTestFixture(_objects);
            var abandoned = career.Start("circuit");

            var retry = career.Service.BeginAttempt("Race", "vanguard");

            Assert.That(retry.AttemptId, Is.Not.EqualTo(abandoned.AttemptId));
            Assert.That(career.State.CompletedTrackIds, Is.Empty, "Starting, quitting or retrying records nothing.");
            Assert.That(career.Service.GetSummary().CompletionPercent, Is.Zero);
        }

        [Test]
        public void FinaleStaysLockedUntilEveryRequirementIsMet()
        {
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);

            var request = career.Service.PrepareRace("crossover", true);

            Assert.That(request.Status, Is.EqualTo(CareerRaceRequestStatus.FinaleLocked));
            Assert.That(career.Service.GetFinaleStatus().MetRequirements, Is.EqualTo(2));
            Assert.That(career.Service.GetFinaleStatus().NextMissingTitle, Is.EqualTo("Buy an Engine upgrade"));
        }

        [Test]
        public void LosingTheFinaleCanBeRetriedAndWinningCompletesTheCareerOnce()
        {
            var career = new CareerTestFixture(_objects);
            career.QualifyForFinale();
            Assert.That(career.Service.GetFinaleStatus().State, Is.EqualTo(CareerFinaleState.Available));
            Assert.That(career.Service.GetSummary().CompletionPercent, Is.EqualTo(88));

            var loss = career.Race("crossover", 2, finale: true);
            Assert.That(loss.IsFinale, Is.True);
            Assert.That(loss.CareerCompleted, Is.False);
            Assert.That(career.State.FinaleWon, Is.False);

            // Retry reloads the same scene: the armed final is reused.
            var retry = career.Service.BeginAttempt("RaceSuzuka", "vanguard");
            Assert.That(retry.IsFinale, Is.True);
            var currencyBefore = career.Progression.Currency;
            var win = career.Service.RecordRaceFinish(retry, new RaceResult(CareerTestFixture.Player, 1, 90f), CareerTestFixture.Player);

            Assert.That(win.CareerCompleted, Is.True);
            Assert.That(win.Awards, Has.Exactly(1).Matches<CareerMilestoneAward>(award => award.IsFinale && award.CreditedReward == 2000));
            Assert.That(career.Progression.Currency, Is.EqualTo(currencyBefore + 2000));
            Assert.That(win.Summary.CompletionPercent, Is.EqualTo(100));
            Assert.That(win.Summary.IsComplete, Is.True);

            var replay = career.Race("crossover", 1, finale: true);
            Assert.That(replay.Status, Is.EqualTo(CareerRaceOutcomeStatus.Recorded), "The player can keep racing after the final.");
            Assert.That(replay.CareerCompleted, Is.False);
            Assert.That(replay.Awards, Is.Empty);
            Assert.That(career.Progression.Currency, Is.EqualTo(currencyBefore + 2000));
        }

        [Test]
        public void ANormalRaceOnTheFinaleCircuitNeverCountsAsTheFinale()
        {
            var career = new CareerTestFixture(_objects);
            career.QualifyForFinale();

            var normal = career.Race("crossover", 1);

            Assert.That(normal.IsFinale, Is.False);
            Assert.That(career.State.FinaleWon, Is.False);
            Assert.That(career.Service.GetSummary().CompletionPercent, Is.LessThan(100));
        }

        [Test]
        public void AStaleFinaleSelectionDoesNotTurnALaterRaceIntoTheFinale()
        {
            var career = new CareerTestFixture(_objects);
            career.QualifyForFinale();
            career.Service.PrepareRace("crossover", true);
            career.Service.CheckRaceStart("RaceSuzuka");

            // A later race start that did not come from the final entry re-arms a normal race.
            career.Service.CheckRaceStart("RaceSuzuka");
            var attempt = career.Service.BeginAttempt("RaceSuzuka", "vanguard");

            Assert.That(attempt.IsFinale, Is.False);
        }

        [Test]
        public void UpgradeMilestonesArePaidOnceAndAnnouncedOnce()
        {
            var career = new CareerTestFixture(_objects);
            career.Progression.SetUpgradeLevel(UpgradeType.Engine, 1);

            var first = career.Service.EvaluateMilestones();
            var second = career.Service.EvaluateMilestones();

            Assert.That(first, Has.Exactly(1).Matches<CareerMilestoneAward>(award => award.MilestoneId == "engine-1" && award.CreditedReward == 150));
            Assert.That(second, Is.Empty);
            Assert.That(career.Progression.Currency, Is.EqualTo(150));
            Assert.That(career.Service.ConsumeAnnouncements().Count, Is.EqualTo(1));
            Assert.That(career.Service.ConsumeAnnouncements(), Is.Empty);
        }

        [Test]
        public void UnknownAndDuplicateIdsInStateAreToleratedWithoutLosingValidProgress()
        {
            var state = new CareerState();
            state.Restore(
                new[] { "vanguard", "vanguard", " striker ", "retired-car", "" },
                new[] { "circuit", "old-track" },
                new[] { "circuit" },
                new[] { "apex-finish", "apex-win", "apex-win", "removed-milestone" },
                false);
            var career = new CareerTestFixture(_objects, state: state);

            var summary = career.Service.GetSummary();

            Assert.That(state.OwnedVehicleIds, Is.EqualTo(new[] { "vanguard", "striker", "retired-car" }));
            Assert.That(summary.OwnedVehicles, Is.EqualTo(2), "Unknown cars are kept but not counted.");
            Assert.That(summary.AchievedMilestones, Is.EqualTo(3), "apex-finish, apex-win and own-two-cars (striker owned).");
            Assert.That(career.Service.IsTrackUnlocked("dunes"), Is.True);
            Assert.That(state.AchievedMilestoneIds, Does.Contain("removed-milestone"));
        }
    }
}
