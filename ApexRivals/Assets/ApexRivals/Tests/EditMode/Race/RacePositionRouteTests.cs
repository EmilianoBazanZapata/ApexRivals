using System.Linq;
using ApexRivals.AI.Runtime;
using ApexRivals.Race.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Race
{
    public sealed class RacePositionRouteTests
    {
        private static readonly Vector3[] Loop =
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, 100),
            new Vector3(10, 0, 100), new Vector3(10, 0, 0)
        };

        [Test]
        public void HairpinRanksByTravelledRouteInsteadOfStraightDistance()
        {
            var checkpoints = new[] { Loop[0], Loop[3] };
            var route = new RacePositionRoute(checkpoints, Loop);
            var earlier = new RacerProgress("Earlier", 2, 0);
            var later = new RacerProgress("Later", 2, 0);
            Assert.That(route.IsValid, Is.True);
            Assert.That(RaceRules.CompareRacePosition(
                new RacerPositionSnapshot(later, new Vector3(0, 0, 80)),
                new RacerPositionSnapshot(earlier, new Vector3(0, 0, 10)), checkpoints, route), Is.LessThan(0));
        }

        [Test]
        public void SectorRestrictionPreventsJumpingToAnotherPartOfTheLoop()
        {
            var route = new RacePositionRoute(Loop, Loop);
            // This position is close to the return straight, but CP1 is still required.
            Assert.That(route.GetSectorProgress(1, new Vector3(10, 0, 40)), Is.EqualTo(40f).Within(0.001f));
        }

        [Test]
        public void FinishLineWrapAndElevationDoNotReverseProgress()
        {
            var route = new RacePositionRoute(Loop, Loop);
            Assert.That(route.GetSectorProgress(0, new Vector3(2, 100, 0)), Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void InvalidRouteFallsBackToExistingCheckpointRanking()
        {
            Assert.That(new RacePositionRoute(Loop, null).IsValid, Is.False);
            Assert.That(new RacePositionRoute(Loop, new[] { Vector3.zero, Vector3.zero }).IsValid, Is.False);
            Assert.That(new RacePositionRoute(new[] { Loop[0], Loop[2], Loop[1], Loop[3] }, Loop).IsValid, Is.False);
        }

        [Test]
        public void DiscreteCheckpointProgressStillTakesPriorityOverRouteProjection()
        {
            var ahead = new RacerProgress("Ahead", 4, 0);
            ahead.PassCheckpoint(1, 4, 3, 1f, 1);
            var behind = new RacerProgress("Behind", 4, 0);
            Assert.That(RaceRules.CompareRacePosition(new RacerPositionSnapshot(ahead, Vector3.zero),
                new RacerPositionSnapshot(behind, Loop[1]), Loop, new RacePositionRoute(Loop, Loop)), Is.LessThan(0));
        }

        [Test]
        public void RealRaceCurveRanksForwardProgressAcrossLateralOffsets()
        {
            var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/Race.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var line = roots.SelectMany(r => r.GetComponentsInChildren<RacingLine>(true)).Single();
                var checkpoints = roots.SelectMany(r => r.GetComponentsInChildren<RaceCheckpoint>(true))
                    .OrderBy(cp => cp.CheckpointIndex).Select(cp => cp.Position).ToArray();
                var points = Enumerable.Range(0, line.WaypointCount).Select(line.GetWaypointPosition).ToArray();
                var route = new RacePositionRoute(checkpoints, points);
                Assert.That(route.IsValid, Is.True);
                // Find every waypoint segment where straight-line distance to the next checkpoint ranks a car
                // that is further along behind one on the opposite side of the road, and check the route fixes it.
                var inversions = 0;
                for (var cp = 0; cp < checkpoints.Length; cp++)
                {
                    var consumed = (cp - 1 + checkpoints.Length) % checkpoints.Length;
                    var first = line.GetNearestWaypointIndex(checkpoints[consumed]);
                    var last = line.GetNearestWaypointIndex(checkpoints[cp]);
                    var count = (last - first + points.Length) % points.Length;
                    for (var i = 0; i < count; i++)
                    {
                        var start = points[(first + i) % points.Length];
                        var end = points[(first + i + 1) % points.Length];
                        foreach (var offset in new[] { 5f, -5f })
                        {
                            var side = Vector3.Cross(Vector3.up, (end - start).normalized);
                            var earlier = Vector3.Lerp(start, end, 0.4f) - side * offset;
                            var later = Vector3.Lerp(start, end, 0.6f) + side * offset;
                            var left = new RacerPositionSnapshot(new RacerProgress("Later", checkpoints.Length, consumed), later);
                            var right = new RacerPositionSnapshot(new RacerProgress("Earlier", checkpoints.Length, consumed), earlier);
                            if (RaceRules.CompareRacePosition(left, right, checkpoints) <= 0)
                            {
                                continue;
                            }

                            inversions++;
                            Assert.That(RaceRules.CompareRacePosition(left, right, checkpoints, route), Is.LessThan(0), $"Sector {cp}, waypoint {(first + i) % points.Length}");
                        }
                    }
                }

                Assert.That(inversions, Is.GreaterThan(0), "Reproduce original Euclidean inversion");
                // Cover every authored waypoint in every checkpoint sector, including wrap.
                for (var cp = 0; cp < checkpoints.Length; cp++)
                {
                    var next = (cp + 1) % checkpoints.Length;
                    var first = line.GetNearestWaypointIndex(checkpoints[cp]);
                    var last = line.GetNearestWaypointIndex(checkpoints[next]);
                    var count = (last - first + points.Length) % points.Length;
                    var previous = -1f;
                    for (var i = 1; i < count; i++)
                    {
                        var progress = route.GetSectorProgress(next, points[(first + i) % points.Length]);
                        Assert.That(progress, Is.GreaterThanOrEqualTo(previous), "Sector " + next);
                        previous = progress;
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
