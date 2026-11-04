using System;
using ApexRivals.AI.Configuration;
using ApexRivals.Progression.Runtime;
using UnityEngine;

namespace ApexRivals.Progression.Configuration
{
    [Serializable]
    public struct RaceProgressionTierDefinition
    {
        [SerializeField] private string tierId;
        [SerializeField] private string displayName;
        [SerializeField, Min(0)] private int requiredCompletedRaceCount;
        [SerializeField] private string aiConfigurationId;
        [SerializeField] private AiDriverConfiguration aiConfiguration;

        public string TierId => tierId;
        public string DisplayName => displayName;
        public int RequiredCompletedRaceCount => requiredCompletedRaceCount;
        public string AiConfigurationId => aiConfigurationId;
        public AiDriverConfiguration AiConfiguration => aiConfiguration;

        public RaceProgressionTier CreateRuntimeTier()
        {
            return new RaceProgressionTier(tierId, displayName, requiredCompletedRaceCount, aiConfigurationId);
        }
    }
}
