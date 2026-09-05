using ApexRivals.AI.Configuration;
using UnityEngine;

namespace ApexRivals.RaceSetup.Configuration
{
    [System.Serializable]
    public struct RaceAiOpponentDefinition
    {
        [SerializeField] private string participantId;
        [SerializeField] private string vehicleId;
        [SerializeField, Min(0)] private int startingGridIndex;
        [SerializeField] private string aiConfigurationId;
        [SerializeField] private AiDriverConfiguration aiConfiguration;

        public string ParticipantId => participantId;
        public string VehicleId => vehicleId;
        public int StartingGridIndex => startingGridIndex;
        public string AiConfigurationId => aiConfigurationId;
        public AiDriverConfiguration AiConfiguration => aiConfiguration;
    }
}
