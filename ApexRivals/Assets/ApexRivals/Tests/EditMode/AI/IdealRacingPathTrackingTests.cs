using ApexRivals.AI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class IdealRacingPathTrackingTests
    {
        private const int Window = 12;
        private const float RelocateDistance = 15f;

        private GameObject _root;
        private IdealRacingPath _path;

        [SetUp]
        public void SetUp()
        {
            // Two parallel straights 8 m apart joined by hairpins: a lap of about 260 m.
            _root = new GameObject("TrackingLine");
            var racingLine = _root.AddComponent<RacingLine>();
            var points = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 80f), new Vector3(0f, 0f, 120f),
                new Vector3(4f, 0f, 124f), new Vector3(8f, 0f, 120f), new Vector3(8f, 0f, 0f), new Vector3(4f, 0f, -4f)
            };
            var waypoints = new Transform[points.Length];
            for (var index = 0; index < points.Length; index++)
            {
                waypoints[index] = new GameObject($"Waypoint_{index}").transform;
                waypoints[index].SetParent(_root.transform);
                waypoints[index].position = points[index];
            }

            racingLine.Configure(waypoints);
            Assert.That(IdealRacingPath.TryBuild(racingLine, 4f, 40f, 8f, 18f, out _path), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void RacerResetFarAhead_IsRelocatedInsteadOfStuckAtTheOldAnchor()
        {
            var anchor = _path.FindNearestSegment(new Vector3(0f, 0f, 2f));
            var resetPosition = new Vector3(0f, 0f, 100f);

            Assert.That(_path.TryProjectLocal(resetPosition, anchor, Window, Window, out var stale), Is.True);
            Assert.That(stale.Distance, Is.GreaterThan(RelocateDistance), "The old window cannot reach the reset position.");

            Assert.That(_path.TryProjectTracked(resetPosition, anchor, Window, RelocateDistance, out var tracked), Is.True);
            Assert.That(tracked.Distance, Is.LessThan(1f));
            Assert.That(tracked.DistanceAlongLap, Is.EqualTo(100f).Within(5f));
        }

        [Test]
        public void RacerOnTrack_StaysOnItsStraightNotTheParallelOne()
        {
            var position = new Vector3(5f, 0f, 60f);
            var anchor = _path.FindNearestSegment(new Vector3(0f, 0f, 56f));

            Assert.That(_path.TryProjectTracked(position, anchor, Window, RelocateDistance, out var tracked), Is.True);

            Assert.That(tracked.DistanceAlongLap, Is.EqualTo(60f).Within(5f), "Still the outbound straight, although it is now closer to the return one.");
        }

        [Test]
        public void NewRacerWithoutAnchor_IsFoundAnywhereOnTheLap()
        {
            Assert.That(_path.TryProjectTracked(new Vector3(8f, 0f, 60f), -1, Window, RelocateDistance, out var tracked), Is.True);

            Assert.That(tracked.Distance, Is.LessThan(1f));
            Assert.That(tracked.DistanceAlongLap, Is.GreaterThan(_path.LapLength * 0.5f), "On the return straight.");
        }
    }
}
