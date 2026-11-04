using System;
using System.Collections.Generic;

namespace ApexRivals.Tracks.Runtime
{
    /// <summary>
    /// Ordered list of raceable tracks. Entries without an id or scene, and repeated ids, are skipped so a
    /// half-authored track never reaches the selection screen.
    /// </summary>
    public sealed class TrackCatalog
    {
        private readonly List<TrackDefinitionData> _tracks = new List<TrackDefinitionData>();

        public TrackCatalog(IEnumerable<TrackDefinitionData> tracks, string defaultTrackId = null)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            if (tracks != null)
            {
                foreach (var track in tracks)
                {
                    if (track == null
                        || string.IsNullOrWhiteSpace(track.TrackId)
                        || string.IsNullOrWhiteSpace(track.SceneName)
                        || !seenIds.Add(track.TrackId))
                    {
                        continue;
                    }

                    _tracks.Add(track);
                }
            }

            var defaultIndex = IndexOf(defaultTrackId);
            DefaultTrackIndex = defaultIndex >= 0 ? defaultIndex : (_tracks.Count > 0 ? 0 : -1);
        }

        public IReadOnlyList<TrackDefinitionData> Tracks => _tracks;
        public int Count => _tracks.Count;
        public int DefaultTrackIndex { get; }
        public TrackDefinitionData DefaultTrack => DefaultTrackIndex >= 0 ? _tracks[DefaultTrackIndex] : null;

        public int IndexOf(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId))
            {
                return -1;
            }

            for (var index = 0; index < _tracks.Count; index++)
            {
                if (_tracks[index].TrackId == trackId)
                {
                    return index;
                }
            }

            return -1;
        }

        public int IndexOfScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return -1;
            }

            for (var index = 0; index < _tracks.Count; index++)
            {
                if (_tracks[index].SceneName == sceneName)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>Wraps <paramref name="index"/> into the catalog range; -1 when the catalog is empty.</summary>
        public int WrapIndex(int index)
        {
            return _tracks.Count == 0 ? -1 : (index % _tracks.Count + _tracks.Count) % _tracks.Count;
        }
    }
}
