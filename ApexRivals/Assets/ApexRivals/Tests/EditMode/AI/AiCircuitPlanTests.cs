using ApexRivals.AI.Configuration;
using ApexRivals.AI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.AI
{
    public sealed class AiCircuitPlanTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("CircuitPlanTest");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private IdealRacingPath Build(params Vector3[] positions)
        {
            var lineObject = new GameObject("Line");
            lineObject.transform.SetParent(_root.transform);
            var line = lineObject.AddComponent<RacingLine>();
            var points = new Transform[positions.Length];
            for (var index = 0; index < positions.Length; index++)
            {
                points[index] = new GameObject("Waypoint").transform;
                points[index].SetParent(lineObject.transform);
                points[index].position = positions[index];
            }
            line.Configure(points);
            Assert.That(IdealRacingPath.TryBuild(line, 4f, 40f, 8f, 4f, 6f, out var path), Is.True);
            return path;
        }

        private IdealRacingPath Square() => Build(Vector3.zero, new Vector3(0f, 0f, 120f),
            new Vector3(120f, 0f, 120f), new Vector3(120f, 0f, 0f));

        [Test]
        public void Square_HasFourRightCornersIncludingTheFinishSeam()
        {
            var path = Square();
            var plan = AiCircuitPlan.Build(path, 40f, 6f);
            Assert.That(plan.Corners.Count, Is.EqualTo(4));
            foreach (var corner in plan.Corners)
            {
                Assert.That(corner.SignedAngle, Is.EqualTo(90f).Within(0.1f));
                Assert.That(corner.Radius, Is.GreaterThan(0f));
                var length = Mathf.Repeat(corner.ExitDistance - corner.EntryDistance, path.LapLength);
                Assert.That(Mathf.Repeat(corner.ApexDistance - corner.EntryDistance, path.LapLength), Is.LessThan(length));
            }
            Assert.That(plan.TryGetCorner(path.LapLength - 2f, 20f, out var before, out _, out _), Is.True);
            Assert.That(plan.TryGetCorner(2f, 20f, out var after, out _, out _), Is.True);
            Assert.That(before, Is.EqualTo(after), "The finish line must not split a corner.");
        }

        [Test]
        public void Chicane_PreservesOppositeCorners()
        {
            var path = Build(Vector3.zero, new Vector3(0f, 0f, 100f), new Vector3(40f, 0f, 100f),
                new Vector3(40f, 0f, 200f), new Vector3(160f, 0f, 200f), new Vector3(160f, 0f, 0f));
            var plan = AiCircuitPlan.Build(path, 40f, 6f);
            var left = false;
            var right = false;
            foreach (var corner in plan.Corners)
            {
                left |= corner.SignedAngle < 0f;
                right |= corner.SignedAngle > 0f;
            }
            Assert.That(left && right, Is.True, "An S bend must remain two opposed turns.");
        }

        [Test]
        public void TightChicane_DoesNotCancelIntoStraightLineSpeed()
        {
            var path = Build(Vector3.zero, new Vector3(0f, 0f, 100f), new Vector3(12f, 0f, 100f),
                new Vector3(12f, 0f, 200f), new Vector3(120f, 0f, 200f), new Vector3(120f, 0f, 0f));
            var plan = AiCircuitPlan.Build(path, 40f, 6f);
            var position = new Vector3(6f, 0f, 100f);
            Assert.That(path.TryProjectLocal(position, path.FindNearestSegment(position), 2, 2, out var projection), Is.True);
            Assert.That(plan.GetSpeed(projection, 4.5f, 1f), Is.LessThan(12f), "Opposite tight turns must both constrain the speed.");
        }

        [Test]
        public void SpeedPlan_BrakesBeforeTheCornerAndReleasesOnExit()
        {
            var path = Square();
            var plan = AiCircuitPlan.Build(path, 40f, 6f);
            float Speed(float z)
            {
                var position = new Vector3(0f, 0f, z);
                Assert.That(path.TryProjectLocal(position, path.FindNearestSegment(position), 2, 2, out var projection), Is.True);
                return plan.GetSpeed(projection, 4.5f, 1f);
            }
            Assert.That(Speed(60f), Is.GreaterThan(Speed(100f)));
            Assert.That(Speed(116f), Is.LessThan(15f));
            var previous = Speed(60f);
            for (var z = 61f; z <= 100f; z++)
            {
                var next = Speed(z);
                Assert.That(previous * previous, Is.LessThanOrEqualTo(next * next + 9.01f), "Every metre must remain brake-feasible.");
                previous = next;
            }
            Assert.That(Speed(119.99f), Is.EqualTo(Speed(120f)).Within(0.1f));
        }

        [Test]
        public void FlagOff_PreservesExistingTrackerAndFlagOnBuildsTheMap()
        {
            var configuration = ScriptableObject.CreateInstance<AiDriverConfiguration>();
            try
            {
                Square();
                var line = _root.GetComponentInChildren<RacingLine>();
                var tracker = new AiPathTracker();
                Assert.That(tracker.TryBuild(line, 4f, configuration, Vector3.zero, null), Is.True);
                Assert.That(tracker.CircuitPlan, Is.Null);
                JsonUtility.FromJsonOverwrite("{\"enableCircuitPlan\":true}", configuration);
                Assert.That(tracker.TryBuild(line, 4f, configuration, Vector3.zero, null), Is.True);
                Assert.That(tracker.CircuitPlan.Corners.Count, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(configuration);
            }
        }
    }
}
