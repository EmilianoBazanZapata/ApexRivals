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
}
