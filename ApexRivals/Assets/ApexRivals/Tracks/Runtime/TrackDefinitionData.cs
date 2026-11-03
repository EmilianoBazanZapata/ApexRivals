using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Tracks.Runtime
{
    /// <summary>Immutable, scene-independent description of one selectable track.</summary>
    public sealed class TrackDefinitionData
    {
        public TrackDefinitionData(
            string trackId,
            string displayName,
            string location,
            float lengthMeters,
            int laps,
            string environment,
            string surface,
            string sceneName,
            Sprite preview = null,
            IReadOnlyList<Vector2> minimapPoints = null,
            bool finaleOnly = false)
        {
            FinaleOnly = finaleOnly;
            TrackId = trackId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Location = location ?? string.Empty;
            LengthMeters = Mathf.Max(0f, lengthMeters);
            Laps = Mathf.Max(1, laps);
            Environment = environment ?? string.Empty;
            Surface = surface ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            Preview = preview;
            MinimapPoints = minimapPoints ?? Array.Empty<Vector2>();
        }

        public string TrackId { get; }
        public string DisplayName { get; }
        public string Location { get; }
        public float LengthMeters { get; }
        public int Laps { get; }
        public string Environment { get; }
        public string Surface { get; }
        public string SceneName { get; }
        public Sprite Preview { get; }

        /// <summary>Closed racing line in normalized minimap space; index 0 is the start/finish line.</summary>
        public IReadOnlyList<Vector2> MinimapPoints { get; }

        /// <summary>Raced only as the career final; never listed or counted as a regular circuit.</summary>
        public bool FinaleOnly { get; }
    }
}
