using System.Collections.Generic;
using ApexRivals.Tracks.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class RaceMinimapTests
    {
        private static readonly Vector2[] Square =
        {
            new Vector2(0f, 0f), new Vector2(0f, 100f), new Vector2(100f, 100f), new Vector2(100f, 0f)
        };

        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

        [Test]
        public void Frame_MapsWorldPointsExactlyLikeTheOutline()
        {
            var wide = new[] { new Vector2(-50f, 10f), new Vector2(150f, 10f), new Vector2(150f, 60f), new Vector2(-50f, 60f) };

            var frame = TrackMinimap.Fit(wide);
            var outline = TrackMinimap.Normalize(wide);

            for (var index = 0; index < wide.Length; index++)
            {
                Assert.That(Vector2.Distance(frame.ToNormalized(wide[index]), outline[index]), Is.LessThan(0.0001f));
            }

            Assert.That(frame.ToNormalized(new Vector2(50f, 35f)), Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(TrackMinimap.Fit(null).IsValid, Is.False);
        }

        [Test]
        public void Graphic_MapsNormalizedPointsInsideItsRect()
        {
            var track = CreateTrack(new Vector2(200f, 200f));
            Assert.That(track.TryMapNormalized(Vector2.zero, out _), Is.False);

            track.SetPoints(TrackMinimap.Normalize(Square));

            Assert.That(track.TryMapNormalized(new Vector2(0.5f, 0.5f), out var centre), Is.True);
            Assert.That(centre, Is.EqualTo(Vector2.zero));
            Assert.That(track.TryMapNormalized(Vector2.one, out var corner), Is.True);
            Assert.That(corner.x, Is.GreaterThan(0f).And.LessThan(100f));
            Assert.That(corner.y, Is.GreaterThan(0f).And.LessThan(100f));
        }

        [Test]
        public void View_PlacesOneMarkerPerCarWithThePlayerOnTop()
        {
            var view = CreateView(out var track);
            var source = new FakeSource(Square);
            source.Cars.Add((new Vector2(100f, 100f), true));
            source.Cars.Add((new Vector2(0f, 0f), false));
            source.Cars.Add((new Vector2(50f, 0f), false));

            view.Bind(source);

            var player = track.transform.Find("PlayerMarker");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.gameObject.activeSelf, Is.True);
            Assert.That(player.GetSiblingIndex(), Is.EqualTo(track.transform.childCount - 1));
            Assert.That(track.TryMapNormalized(TrackMinimap.Fit(Square).ToNormalized(new Vector2(100f, 100f)), out var expected), Is.True);
            Assert.That(Vector2.Distance(player.localPosition, expected), Is.LessThan(0.001f));
            Assert.That(ActiveMarkers(track, "RivalMarker_"), Is.EqualTo(2));
        }

        [Test]
        public void View_HidesMarkersForCarsThatLeftAndOnUnbind()
        {
            var view = CreateView(out var track);
            var source = new FakeSource(Square);
            source.Cars.Add((new Vector2(0f, 0f), false));
            source.Cars.Add((new Vector2(50f, 0f), false));
            source.Cars.Add((new Vector2(100f, 0f), true));
            view.Bind(source);

            source.Cars.RemoveAt(1);
            view.Bind(source);
            Assert.That(ActiveMarkers(track, "RivalMarker_"), Is.EqualTo(1));

            view.Bind(null);
            Assert.That(ActiveMarkers(track, "RivalMarker_"), Is.Zero);
            Assert.That(track.transform.Find("PlayerMarker").gameObject.activeSelf, Is.False);
            Assert.That(track.TryMapNormalized(Vector2.zero, out _), Is.False);
        }

        private TrackMinimapGraphic CreateTrack(Vector2 size)
        {
            var go = _objects.Own(new GameObject("MinimapTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(TrackMinimapGraphic)));
            ((RectTransform)go.transform).sizeDelta = size;
            return go.GetComponent<TrackMinimapGraphic>();
        }

        private RaceMinimapUguiView CreateView(out TrackMinimapGraphic track)
        {
            track = CreateTrack(new Vector2(240f, 240f));
            var rival = Marker(track.transform, "RivalMarkerTemplate");
            var player = Marker(track.transform, "PlayerMarkerTemplate");
            var view = _objects.Own(new GameObject("MinimapPanel")).AddComponent<RaceMinimapUguiView>();
            var refs = new SerializedObject(view);
            refs.FindProperty("track").objectReferenceValue = track;
            refs.FindProperty("rivalMarkerTemplate").objectReferenceValue = rival;
            refs.FindProperty("playerMarkerTemplate").objectReferenceValue = player;
            refs.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static RectTransform Marker(Transform parent, string name)
        {
            var marker = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            marker.transform.SetParent(parent, false);
            marker.SetActive(false);
            return (RectTransform)marker.transform;
        }

        private static int ActiveMarkers(TrackMinimapGraphic track, string prefix)
        {
            var count = 0;
            foreach (Transform child in track.transform)
            {
                if (child.name.StartsWith(prefix) && child.gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class FakeSource : IRaceMinimapSource
        {
            public FakeSource(IReadOnlyList<Vector2> trackLine)
            {
                TrackLine = trackLine;
            }

            public IReadOnlyList<Vector2> TrackLine { get; }
            public List<(Vector2 position, bool isPlayer)> Cars { get; } = new List<(Vector2 position, bool isPlayer)>();
            public int CarCount => Cars.Count;

            public bool TryGetCar(int index, out Vector2 position, out bool isPlayer)
            {
                position = Cars[index].position;
                isPlayer = Cars[index].isPlayer;
                return true;
            }
        }
    }
}
