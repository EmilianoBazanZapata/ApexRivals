using System.Collections.Generic;
using ApexRivals.AI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class RacingLineRecoveryTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
        }

        [Test]
        public void TryGetNearestPoint_OffTrackPositionReturnsNearestSegmentAndRouteForward()
        {
            var lineObject = NewObject("RacingLine");
            var line = lineObject.AddComponent<RacingLine>();
            var first = NewObject("Waypoint0").transform;
            var second = NewObject("Waypoint1").transform;
            var third = NewObject("Waypoint2").transform;
            first.position = new Vector3(0f, 0f, 0f);
            second.position = new Vector3(10f, 0f, 0f);
            third.position = new Vector3(10f, 0f, 10f);
            line.Configure(new[] { first, second, third });

            var found = line.TryGetNearestPoint(new Vector3(5f, 0f, 1f), out var index, out var point, out var forward, out var distance);

            Assert.That(found, Is.True);
            Assert.That(index, Is.EqualTo(0));
            Assert.That(point, Is.EqualTo(new Vector3(5f, 0f, 0f)));
            Assert.That(distance, Is.EqualTo(1f).Within(0.001f));
            Assert.That(forward, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void TryGetNearestPointInProgressRange_HairpinParallelBranchRejectsGlobalNearestBranch()
        {
            var lineObject = NewObject("RacingLine");
            var line = lineObject.AddComponent<RacingLine>();
            var waypoints = new Transform[8];
            for (var index = 0; index < waypoints.Length; index++)
            {
                waypoints[index] = NewObject($"Waypoint{index}").transform;
            }

            // Indices 1-3 travel east on the lower hairpin branch; 5-7 travel west
            // on the close upper branch. The sampled position is closer to the upper branch.
            waypoints[0].position = new Vector3(-10f, 0f, 0f);
            waypoints[1].position = new Vector3(0f, 0f, 0f);
            waypoints[2].position = new Vector3(10f, 0f, 0f);
            waypoints[3].position = new Vector3(20f, 0f, 0f);
            waypoints[4].position = new Vector3(20f, 0f, 4f);
            waypoints[5].position = new Vector3(10f, 0f, 4f);
            waypoints[6].position = new Vector3(0f, 0f, 4f);
            waypoints[7].position = new Vector3(-10f, 0f, 4f);
            line.Configure(waypoints);

            var sample = new Vector3(5f, 0f, 3.5f);
            line.TryGetNearestPoint(sample, out var globalIndex, out _, out _, out _);
            var foundProgressPoint = line.TryGetNearestPointInProgressRange(sample, 1, 3, 0, out var progressIndex, out _, out _, out _);

            Assert.That(globalIndex, Is.EqualTo(5));
            Assert.That(foundProgressPoint, Is.True);
            Assert.That(progressIndex, Is.InRange(1, 3));
        }

        [Test]
        public void TryGetArcLookaheadTarget_VariableSpacingUsesArcDistanceInsteadOfWaypointCount()
        {
            var lineObject = NewObject("RacingLine");
            var line = lineObject.AddComponent<RacingLine>();
            var waypoints = new[]
            {
                NewWaypoint("Waypoint0", new Vector3(0f, 0f, 0f)),
                NewWaypoint("Waypoint1", new Vector3(2f, 0f, 0f)),
                NewWaypoint("Waypoint2", new Vector3(12f, 0f, 0f)),
                NewWaypoint("Waypoint3", new Vector3(12f, 0f, 10f))
            };
            line.Configure(waypoints);

            var found = line.TryGetArcLookaheadTarget(Vector3.zero, 0, 7f, 3, out var targetIndex, out var projectedStart, out var target, out var arcDistance);

            Assert.That(found, Is.True);
            Assert.That(projectedStart, Is.EqualTo(Vector3.zero));
            Assert.That(targetIndex, Is.EqualTo(1));
            Assert.That(target.x, Is.EqualTo(7f).Within(0.001f));
            Assert.That(arcDistance, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void TryGetArcLookaheadTarget_HairpinStaysOnImmediateSequentialBranch()
        {
            var lineObject = NewObject("RacingLine");
            var line = lineObject.AddComponent<RacingLine>();
            var waypoints = new[]
            {
                NewWaypoint("Waypoint0", new Vector3(-10f, 0f, 0f)),
                NewWaypoint("Waypoint1", new Vector3(0f, 0f, 0f)),
                NewWaypoint("Waypoint2", new Vector3(10f, 0f, 0f)),
                NewWaypoint("Waypoint3", new Vector3(20f, 0f, 0f)),
                NewWaypoint("Waypoint4", new Vector3(20f, 0f, 4f)),
                NewWaypoint("Waypoint5", new Vector3(10f, 0f, 4f)),
                NewWaypoint("Waypoint6", new Vector3(0f, 0f, 4f)),
                NewWaypoint("Waypoint7", new Vector3(-10f, 0f, 4f))
            };
            line.Configure(waypoints);

            var found = line.TryGetArcLookaheadTarget(new Vector3(5f, 0f, 3.5f), 1, 6f, 3, out var targetIndex, out _, out var target, out _);

            Assert.That(found, Is.True);
            Assert.That(targetIndex, Is.EqualTo(2));
            Assert.That(target.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void TryGetArcLookaheadTarget_WrapsFromFinalSegmentToFirst()
        {
            var lineObject = NewObject("RacingLine");
            var line = lineObject.AddComponent<RacingLine>();
            var waypoints = new[]
            {
                NewWaypoint("Waypoint0", new Vector3(0f, 0f, 0f)),
                NewWaypoint("Waypoint1", new Vector3(10f, 0f, 0f)),
                NewWaypoint("Waypoint2", new Vector3(10f, 0f, 10f)),
                NewWaypoint("Waypoint3", new Vector3(0f, 0f, 10f))
            };
            line.Configure(waypoints);

            var found = line.TryGetArcLookaheadTarget(waypoints[3].position, 3, 5f, 2, out var targetIndex, out _, out var target, out var arcDistance);

            Assert.That(found, Is.True);
            Assert.That(targetIndex, Is.EqualTo(3));
            Assert.That(target.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(target.z, Is.EqualTo(5f).Within(0.001f));
            Assert.That(arcDistance, Is.EqualTo(5f).Within(0.001f));
        }

        private Transform NewWaypoint(string name, Vector3 position)
        {
            var waypoint = NewObject(name).transform;
            waypoint.position = position;
            return waypoint;
        }

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }
    }
}
