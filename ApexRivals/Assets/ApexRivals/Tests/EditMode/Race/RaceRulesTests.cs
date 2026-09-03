using ApexRivals.Race.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Race
{
    public sealed class RaceRulesTests
    {
        [Test]
        public void PassCheckpoint_FirstValidCheckpoint_AdvancesProgress()
        {
            var progress = new RacerProgress("Player");

            var result = progress.PassCheckpoint(0, 3, 3, 2f, 1);

            Assert.That(result.Accepted, Is.True);
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(1));
            Assert.That(progress.TotalPassedCheckpoints, Is.EqualTo(1));
        }

        [Test]
        public void PassCheckpoint_SkippedCheckpoint_IsRejected()
        {
            var progress = new RacerProgress("Player");

            var result = progress.PassCheckpoint(1, 3, 3, 2f, 1);

            Assert.That(result.Status, Is.EqualTo(CheckpointPassStatus.Skipped));
            Assert.That(progress.TotalPassedCheckpoints, Is.EqualTo(0));
        }

        [Test]
        public void PassCheckpoint_DuplicateCheckpoint_IsRejected()
        {
            var progress = new RacerProgress("Player");
            progress.PassCheckpoint(0, 3, 3, 2f, 1);

            var result = progress.PassCheckpoint(0, 3, 3, 3f, 1);

            Assert.That(result.Status, Is.EqualTo(CheckpointPassStatus.Duplicate));
            Assert.That(progress.TotalPassedCheckpoints, Is.EqualTo(1));
        }

        [Test]
        public void PassCheckpoint_FinalOrderedCheckpoint_CompletesLap()
        {
            var progress = new RacerProgress("Player");
            progress.PassCheckpoint(0, 3, 3, 1f, 1);
            progress.PassCheckpoint(1, 3, 3, 2f, 1);

            var result = progress.PassCheckpoint(2, 3, 3, 3f, 1);

            Assert.That(result.LapCompleted, Is.True);
            Assert.That(progress.CompletedLaps, Is.EqualTo(1));
            Assert.That(progress.CurrentLap, Is.EqualTo(2));
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(0));
        }

        [Test]
        public void PassCheckpoint_LapCannotComplete_WhenPriorCheckpointsWereSkipped()
        {
            var progress = new RacerProgress("Player");

            var result = progress.PassCheckpoint(2, 3, 3, 3f, 1);

            Assert.That(result.Status, Is.EqualTo(CheckpointPassStatus.Skipped));
            Assert.That(result.LapCompleted, Is.False);
            Assert.That(progress.CompletedLaps, Is.EqualTo(0));
        }

        [Test]
        public void PassCheckpoint_FinalLap_FinishesRacerExactlyOnce()
        {
            var progress = new RacerProgress("Player");
            progress.PassCheckpoint(0, 1, 1, 10f, 1);

            var duplicate = progress.PassCheckpoint(0, 1, 1, 11f, 2);

            Assert.That(progress.HasFinished, Is.True);
            Assert.That(progress.FinalPosition, Is.EqualTo(1));
            Assert.That(progress.FinishTime, Is.EqualTo(10f));
            Assert.That(duplicate.Status, Is.EqualTo(CheckpointPassStatus.RacerAlreadyFinished));
        }

        [Test]
        public void CompareRacePosition_RanksByCompletedLaps()
        {
            var leader = new RacerProgress("Leader");
            var chaser = new RacerProgress("Chaser");
            leader.PassCheckpoint(0, 1, 3, 1f, 1);

            var comparison = RaceRules.CompareRacePosition(
                new RacerPositionSnapshot(leader, Vector3.zero),
                new RacerPositionSnapshot(chaser, Vector3.zero),
                new[] { Vector3.forward });

            Assert.That(comparison, Is.LessThan(0));
        }

        [Test]
        public void CompareRacePosition_RanksByValidCheckpoints()
        {
            var leader = new RacerProgress("Leader");
            var chaser = new RacerProgress("Chaser");
            leader.PassCheckpoint(0, 3, 3, 1f, 1);

            var comparison = RaceRules.CompareRacePosition(
                new RacerPositionSnapshot(leader, Vector3.zero),
                new RacerPositionSnapshot(chaser, Vector3.zero),
                new[] { Vector3.zero, Vector3.forward, Vector3.forward * 2f });

            Assert.That(comparison, Is.LessThan(0));
        }

        [Test]
        public void CompareRacePosition_RanksByDistanceToNextCheckpoint()
        {
            var near = new RacerProgress("Near");
            var far = new RacerProgress("Far");
            var checkpoints = new[] { Vector3.forward * 10f };

            var comparison = RaceRules.CompareRacePosition(
                new RacerPositionSnapshot(near, Vector3.forward * 9f),
                new RacerPositionSnapshot(far, Vector3.zero),
                checkpoints);

            Assert.That(comparison, Is.LessThan(0));
        }

        [Test]
        public void CompareRacePosition_FinishedRacerStaysAheadOfUnfinishedRacer()
        {
            var finished = new RacerProgress("Finished");
            var racing = new RacerProgress("Racing");
            finished.PassCheckpoint(0, 1, 1, 5f, 1);

            var comparison = RaceRules.CompareRacePosition(
                new RacerPositionSnapshot(finished, Vector3.zero),
                new RacerPositionSnapshot(racing, Vector3.forward * 100f),
                new[] { Vector3.forward });

            Assert.That(comparison, Is.LessThan(0));
        }

        [Test]
        public void CompareRacePosition_FinishOrderIsStable()
        {
            var first = new RacerProgress("First");
            var second = new RacerProgress("Second");
            first.PassCheckpoint(0, 1, 1, 5f, 1);
            second.PassCheckpoint(0, 1, 1, 4f, 2);

            var comparison = RaceRules.CompareRacePosition(
                new RacerPositionSnapshot(first, Vector3.zero),
                new RacerPositionSnapshot(second, Vector3.zero),
                new[] { Vector3.forward });

            Assert.That(comparison, Is.LessThan(0));
        }

        [Test]
        public void ConfigurationRules_ClampInvalidValues()
        {
            Assert.That(RaceRules.ClampTotalLaps(0), Is.EqualTo(1));
            Assert.That(RaceRules.ClampCountdownDuration(-2f), Is.EqualTo(0f));
        }

        [Test]
        public void IsCheckpointSequenceValid_RejectsMissingOrOutOfOrderIndices()
        {
            Assert.That(RaceRules.IsCheckpointSequenceValid(new[] { 0, 1, 2 }), Is.True);
            Assert.That(RaceRules.IsCheckpointSequenceValid(new[] { 0, 2, 1 }), Is.False);
            Assert.That(RaceRules.IsCheckpointSequenceValid(new[] { 1, 2, 3 }), Is.False);
        }
    }
}
