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
        [SerializeField, Min(0f), Tooltip("Scales the vehicle's acceleration and top speed for this opponent. 0 or 1 keeps the vehicle's own stats.")]
        private float performanceMultiplier;

        [SerializeField, Min(0f), Tooltip("Scales this AI car's normal tyre grip. 0 or 1 keeps its original grip; player vehicles are unaffected.")]
        private float handlingMultiplier;

        public string ParticipantId => participantId;
        public string VehicleId => vehicleId;
        public int StartingGridIndex => startingGridIndex;
        public string AiConfigurationId => aiConfigurationId;
        public AiDriverConfiguration AiConfiguration => aiConfiguration;

        /// <summary>Unset entries serialize as 0, which means "use the vehicle's own stats".</summary>
        public float PerformanceMultiplier => performanceMultiplier > 0f ? performanceMultiplier : 1f;
        public float HandlingMultiplier => handlingMultiplier > 0f && !float.IsInfinity(handlingMultiplier) ? handlingMultiplier : 1f;
    }
}
