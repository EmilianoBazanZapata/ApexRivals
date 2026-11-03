using System;
using ApexRivals.Race.Configuration;
using ApexRivals.Tracks.Runtime;
using UnityEngine;

namespace ApexRivals.Tracks.Configuration
{
    [CreateAssetMenu(fileName = "TrackDefinition", menuName = "Apex Rivals/Tracks/Track Definition")]
    public sealed class TrackDefinition : ScriptableObject
    {
        [SerializeField] private string trackId = "track";
        [SerializeField] private string displayName = "New Track";
        [SerializeField, Tooltip("Subtitle shown under the track name, e.g. the area it is set in.")] private string location = string.Empty;
        [SerializeField, Min(0f)] private float lengthMeters;
        [SerializeField, Tooltip("Lap count is read from here when assigned so the screen matches the race.")] private RaceDefinition raceDefinition;
        [SerializeField, Min(1)] private int laps = 3;
        [SerializeField] private string environment = string.Empty;
        [SerializeField] private string surface = "Asphalt";
        [SerializeField, Tooltip("Content scene loaded when this track is raced. Must be in the build settings.")] private string sceneName = "Race";
        [SerializeField] private Sprite preview;
        [SerializeField, Tooltip("Closed racing line in normalized (0-1) space; the first point is the start/finish line.")]
        private Vector2[] minimapPoints = Array.Empty<Vector2>();
        [SerializeField, Tooltip("Raced only as the career final: hidden from the normal track list and not counted as a circuit.")]
        private bool finaleOnly;

        public string TrackId => trackId;
        public string SceneName => sceneName;

        public TrackDefinitionData CreateData()
        {
            return new TrackDefinitionData(
                trackId,
                displayName,
                location,
                lengthMeters,
                raceDefinition != null ? raceDefinition.TotalLaps : laps,
                environment,
                surface,
                sceneName,
                preview,
                minimapPoints,
                finaleOnly);
        }
    }
}
