using System;
using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.Progression.Runtime;
using UnityEngine;

namespace ApexRivals.Progression.Configuration
{
    [CreateAssetMenu(fileName = "RaceProgressionDefinition", menuName = "Apex Rivals/Progression/Race Progression Definition")]
    public sealed class RaceProgressionDefinition : ScriptableObject
    {
        [SerializeField] private RaceProgressionTierDefinition[] tiers = Array.Empty<RaceProgressionTierDefinition>();

        public IReadOnlyList<RaceProgressionTierDefinition> Tiers => tiers;

        public RaceProgressionTable CreateTable()
        {
            var runtimeTiers = new RaceProgressionTier[tiers.Length];
            for (var index = 0; index < tiers.Length; index++)
            {
                runtimeTiers[index] = tiers[index].CreateRuntimeTier();
            }

            return new RaceProgressionTable(runtimeTiers);
        }

        public IReadOnlyDictionary<string, AiDriverConfiguration> CreateAiConfigurationMap()
        {
            var map = new Dictionary<string, AiDriverConfiguration>(StringComparer.Ordinal);
            for (var index = 0; index < tiers.Length; index++)
            {
                var tier = tiers[index];
                if (!string.IsNullOrWhiteSpace(tier.AiConfigurationId) && !map.ContainsKey(tier.AiConfigurationId))
                {
                    map.Add(tier.AiConfigurationId, tier.AiConfiguration);
                }
            }

            return map;
        }
    }

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
