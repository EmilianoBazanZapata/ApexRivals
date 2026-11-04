using ApexRivals.Tracks.Runtime;
using UnityEngine;

namespace ApexRivals.Tracks.Configuration
{
    [CreateAssetMenu(fileName = "TrackCatalogDefinition", menuName = "Apex Rivals/Tracks/Track Catalog")]
    public sealed class TrackCatalogDefinition : ScriptableObject
    {
        [SerializeField] private string defaultTrackId = string.Empty;
        [SerializeField, Tooltip("Shown in this order on the track select screen.")] private TrackDefinition[] tracks = System.Array.Empty<TrackDefinition>();

        public TrackCatalog CreateCatalog()
        {
            var trackData = new TrackDefinitionData[tracks.Length];
            for (var index = 0; index < tracks.Length; index++)
            {
                trackData[index] = tracks[index] != null ? tracks[index].CreateData() : null;
            }

            return new TrackCatalog(trackData, defaultTrackId);
        }
    }
}
