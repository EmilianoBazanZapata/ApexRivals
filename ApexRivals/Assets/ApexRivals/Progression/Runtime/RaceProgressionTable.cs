using System;
using System.Collections.Generic;

namespace ApexRivals.Progression.Runtime
{
    public sealed class RaceProgressionTable
    {
        private readonly IReadOnlyList<RaceProgressionTier> _tiers;
        private readonly RaceProgressionConfigurationResult _validation;

        public RaceProgressionTable(IReadOnlyList<RaceProgressionTier> tiers)
        {
            _tiers = Array.AsReadOnly(tiers != null ? Copy(tiers) : Array.Empty<RaceProgressionTier>());
            // Tiers contain only readonly values and strings. Unity configuration
            // references remain live-validated by RaceProgressionService.
            _validation = ValidateConfiguration();
        }

        public IReadOnlyList<RaceProgressionTier> Tiers => _tiers;

        public RaceProgressionConfigurationResult Validate() => _validation;

        private RaceProgressionConfigurationResult ValidateConfiguration()
        {
            if (_tiers.Count == 0)
            {
                return RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.MissingConfiguration, "Progression tiers are missing.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var previousThreshold = -1;
            var hasInitialTier = false;
            for (var index = 0; index < _tiers.Count; index++)
            {
                var tier = _tiers[index];
                if (string.IsNullOrWhiteSpace(tier.TierId))
                {
                    return RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.MissingConfiguration, "Progression tier ID is missing.");
                }

                if (!ids.Add(tier.TierId))
                {
                    return RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.DuplicateTierId, $"Progression tier ID '{tier.TierId}' is duplicated.");
                }

                if (tier.RequiredCompletedRaceCount < 0)
                {
                    return RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.NegativeThreshold, "Progression tier thresholds cannot be negative.");
                }

                if (tier.RequiredCompletedRaceCount <= previousThreshold)
                {
                    return RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.UnorderedThreshold, "Progression tier thresholds must be strictly increasing.");
                }

                if (string.IsNullOrWhiteSpace(tier.AiConfigurationId))
                {
                    return RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.MissingAiConfiguration, $"Progression tier '{tier.TierId}' has no AI configuration ID.");
                }

                hasInitialTier |= tier.RequiredCompletedRaceCount == 0;
                previousThreshold = tier.RequiredCompletedRaceCount;
            }

            return hasInitialTier
                ? RaceProgressionConfigurationResult.Valid()
                : RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.MissingInitialTier, "Progression configuration requires an initial tier at zero completed races.");
        }

        private static RaceProgressionTier[] Copy(IReadOnlyList<RaceProgressionTier> tiers)
        {
            var copy = new RaceProgressionTier[tiers.Count];
            for (var index = 0; index < tiers.Count; index++)
            {
                copy[index] = tiers[index];
            }

            return copy;
        }
    }
}
