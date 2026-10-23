using ApexRivals.Tracks.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Tracks
{
    public sealed class TrackCatalogTests
    {
        [Test]
        public void Catalog_SkipsNullIncompleteAndDuplicateTracks()
        {
            var catalog = new TrackCatalog(new[]
            {
                Track("harbor", "Harbor"),
                null,
                Track(string.Empty, "Harbor"),
                Track("no-scene", string.Empty),
                Track("harbor", "Duplicate"),
                Track("valley", "Valley")
            });

            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.Tracks[0].SceneName, Is.EqualTo("Harbor"));
            Assert.That(catalog.Tracks[1].TrackId, Is.EqualTo("valley"));
        }

        [Test]
        public void Catalog_DefaultTrackFallsBackToFirstWhenIdIsUnknown()
        {
            var tracks = new[] { Track("harbor", "Harbor"), Track("valley", "Valley") };

            Assert.That(new TrackCatalog(tracks, "valley").DefaultTrackIndex, Is.EqualTo(1));
            Assert.That(new TrackCatalog(tracks, "missing").DefaultTrackIndex, Is.Zero);
            Assert.That(new TrackCatalog(null).DefaultTrackIndex, Is.EqualTo(-1));
            Assert.That(new TrackCatalog(null).DefaultTrack, Is.Null);
        }

        [Test]
        public void Catalog_LooksUpByIdAndSceneAndWrapsIndices()
        {
            var catalog = new TrackCatalog(new[] { Track("harbor", "Harbor"), Track("valley", "Valley"), Track("canyon", "Canyon") });

            Assert.That(catalog.IndexOf("canyon"), Is.EqualTo(2));
            Assert.That(catalog.IndexOf(null), Is.EqualTo(-1));
            Assert.That(catalog.IndexOfScene("Valley"), Is.EqualTo(1));
            Assert.That(catalog.IndexOfScene("Missing"), Is.EqualTo(-1));
            Assert.That(catalog.WrapIndex(-1), Is.EqualTo(2));
            Assert.That(catalog.WrapIndex(3), Is.Zero);
            Assert.That(new TrackCatalog(null).WrapIndex(1), Is.EqualTo(-1));
        }

        [Test]
        public void Definition_ClampsLengthAndLaps()
        {
            var track = new TrackDefinitionData("id", "Name", "Where", -5f, 0, "Port", "Asphalt", "Race");

            Assert.That(track.LengthMeters, Is.Zero);
            Assert.That(track.Laps, Is.EqualTo(1));
            Assert.That(track.MinimapPoints, Is.Empty);
        }

        [Test]
        public void Minimap_ClosedLengthIncludesTheClosingSegment()
        {
            var square = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f), new Vector2(0f, 10f) };

            Assert.That(TrackMinimap.ClosedLength(square), Is.EqualTo(40f).Within(0.001f));
            Assert.That(TrackMinimap.ClosedLength(new[] { Vector2.zero }), Is.Zero);
        }

        [Test]
        public void Minimap_NormalizeFitsUnitSquareKeepsAspectAndStartsAtStartIndex()
        {
            var wide = new[] { new Vector2(100f, 50f), new Vector2(300f, 50f), new Vector2(300f, 150f), new Vector2(100f, 150f) };

            var normalized = TrackMinimap.Normalize(wide, 2);

            Assert.That(normalized.Length, Is.EqualTo(4));
            // Width 200 fills the square; height 100 is centred vertically (0.25 .. 0.75).
            Assert.That(normalized[0].x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(normalized[0].y, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(normalized[2].x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(normalized[2].y, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Minimap_NormalizeHandlesEmptyAndDegenerateInput()
        {
            Assert.That(TrackMinimap.Normalize(null), Is.Empty);
            var single = TrackMinimap.Normalize(new[] { new Vector2(5f, 5f), new Vector2(5f, 5f) });
            Assert.That(single[0], Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        private static TrackDefinitionData Track(string id, string scene)
        {
            return new TrackDefinitionData(id, id, "Somewhere", 1900f, 3, "Port", "Asphalt", scene);
        }
    }
}
