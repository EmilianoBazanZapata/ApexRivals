using System.Linq;
using ApexRivals.AI.Runtime;
using ApexRivals.Race.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Race
{
    public sealed class RaceRecoveryRouteTests
    {
        private static readonly Vector3[] Loop = { Vector3.zero, new Vector3(0,0,100), new Vector3(100,0,100), new Vector3(100,0,0) };

        [Test]
        public void TrackEligibilityIncludesTheGridBeforeTheConsumedStartCheckpoint()
        {
            var route = new RaceRecoveryRoute(Loop, Loop);
            var grid = new Vector3(20,1,0);
            Assert.That(route.TryGetNearestTrackPoint(grid, out var nearest), Is.True);
            Assert.That(Vector3.Distance(grid, nearest), Is.EqualTo(1).Within(.001f));
            Assert.That(route.TryProject(1, grid, out var recovery, out _, out _), Is.True);
            Assert.That(recovery.x, Is.Zero, "Recovery still cannot advance onto an unvalidated sector.");
        }

        [Test]
        public void RecoveryStaysInTheValidatedSectorAndFacesTheDrivingDirection()
        {
            var route = new RaceRecoveryRoute(Loop, Loop);
            Assert.That(route.TryProject(3, new Vector3(102,.5f,30), out var point, out var rotation, out _), Is.True);
            Assert.That(Vector3.Distance(point, new Vector3(100,0,30)), Is.LessThan(.001f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, Vector3.back), Is.GreaterThan(.99f));
            Assert.That(route.TryProject(1, new Vector3(100,0,30), out point, out _, out var distance), Is.True);
            Assert.That(point.x, Is.Zero, "A closer return straight must not bypass the next required checkpoint.");
            Assert.That(distance, Is.EqualTo(100).Within(.001f));
        }

        [Test]
        public void RecoveryStaysBeforeAnUnacceptedCheckpointAndHandlesFinishWrap()
        {
            var route = new RaceRecoveryRoute(Loop, Loop);
            Assert.That(route.TryProject(1, new Vector3(0,0,110), out var point, out _, out _), Is.True);
            Assert.That(point.z, Is.EqualTo(95).Within(.001f));
            Assert.That(route.TryProject(0, new Vector3(30,0,0), out point, out var rotation, out _), Is.True);
            Assert.That(point.x, Is.EqualTo(30).Within(.001f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, Vector3.left), Is.GreaterThan(.99f));
        }

        [Test]
        public void CrossoversRetainTheirOwnDeckHeight()
        {
            var loop = new[] { new Vector3(-50,0,0), new Vector3(50,0,0), new Vector3(50,12,50),
                new Vector3(0,12,50), new Vector3(0,12,-50), new Vector3(-50,0,-50) };
            var route = new RaceRecoveryRoute(loop, loop);
            Assert.That(route.IsValid, Is.True);
            Assert.That(route.TryProject(4, new Vector3(0,13,0), out var bridge, out _, out _), Is.True);
            Assert.That(bridge.y, Is.EqualTo(12));
            Assert.That(route.TryProject(1, new Vector3(0,1,0), out var underpass, out _, out _), Is.True);
            Assert.That(underpass.y, Is.Zero);
        }

        [Test]
        public void InvalidOrOutOfOrderRoutesAreRejected()
        {
            Assert.That(new RaceRecoveryRoute(Loop, null).IsValid, Is.False);
            Assert.That(new RaceRecoveryRoute(Loop, new[] { Vector3.zero, Vector3.zero, Vector3.zero }).IsValid, Is.False);
            Assert.That(new RaceRecoveryRoute(new[] { Loop[0], Loop[2], Loop[1], Loop[3] }, Loop).IsValid, Is.False);
        }

        [TestCase("Race")]
        [TestCase("RaceDunes")]
        [TestCase("RaceSuzuka")]
        public void AuthoredTracksProvideValidThreeDimensionalRecoverySectors(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + name + ".unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var line = roots.SelectMany(r => r.GetComponentsInChildren<RacingLine>(true)).Single();
                var checkpoints = roots.SelectMany(r => r.GetComponentsInChildren<RaceCheckpoint>(true)).OrderBy(c => c.CheckpointIndex).ToArray();
                var points = Enumerable.Range(0, line.WaypointCount).Select(line.GetWaypointPosition).ToArray();
                var route = new RaceRecoveryRoute(checkpoints.Select(c => c.Position).ToArray(), points);
                Assert.That(route.IsValid, Is.True, name);
                foreach (var cp in checkpoints)
                {
                    Assert.That(route.TryProject((cp.CheckpointIndex + 1) % checkpoints.Length, cp.Position, out var point, out _, out var distance), Is.True);
                    Assert.That(distance, Is.LessThan(2), name + " CP " + cp.CheckpointIndex);
                    Assert.That(Mathf.Abs(point.y - cp.Position.y), Is.LessThan(2));
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
