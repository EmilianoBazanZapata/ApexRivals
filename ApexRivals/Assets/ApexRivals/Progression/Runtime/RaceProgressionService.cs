using System;
using System.Collections.Generic;
using ApexRivals.AI.Configuration;

namespace ApexRivals.Progression.Runtime
{
    public sealed class RaceProgressionService : IRaceDifficultyProvider
    {
        private readonly PlayerProgressionState _progressionState;
        private readonly RaceProgressionTable _table;
        private readonly IReadOnlyDictionary<string, AiDriverConfiguration> _aiConfigurationsById;
        private readonly DifficultyTierResolver _resolver;

        public RaceProgressionService(
            PlayerProgressionState progressionState,
            RaceProgressionTable table,
            IReadOnlyDictionary<string, AiDriverConfiguration> aiConfigurationsById,
            DifficultyTierResolver resolver = null)
        {
            _progressionState = progressionState ?? throw new ArgumentNullException(nameof(progressionState));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aiConfigurationsById = aiConfigurationsById ?? throw new ArgumentNullException(nameof(aiConfigurationsById));
            _resolver = resolver ?? new DifficultyTierResolver();
        }

        public event Action<RaceProgressionChangedEvent> RaceProgressionChanged;
        public event Action<DifficultyTierChangedEvent> DifficultyTierChanged;

        public RaceProgressionConfigurationResult Validate()
        {
            var validation = _table.Validate();
            if (!validation.Succeeded)
            {
                return validation;
            }

            var tiers = _table.Tiers;
            for (var index = 0; index < tiers.Count; index++)
            {
                var aiConfigurationId = tiers[index].AiConfigurationId;
                if (!_aiConfigurationsById.TryGetValue(aiConfigurationId, out var aiConfiguration) || aiConfiguration == null)
                {
                    return RaceProgressionConfigurationResult.Failure(
                        RaceProgressionConfigurationStatus.MissingAiConfiguration,
                        $"Progression tier '{tiers[index].TierId}' references missing AI configuration '{aiConfigurationId}'.");
                }
            }

            return RaceProgressionConfigurationResult.Valid();
        }

        public DifficultyTierResolutionResult ResolveCurrentTier()
        {
            return _resolver.Resolve(_progressionState.CompletedRaceCount, _table);
        }

        public RaceProgressionResult RegisterCompletedPlayerRace()
        {
            var previousCount = _progressionState.CompletedRaceCount;
            var previousTier = _resolver.Resolve(previousCount, _table);
            if (!previousTier.Succeeded)
            {
                return RaceProgressionResult.Failure(previousCount, previousTier.Message);
            }

            var currentTier = _resolver.Resolve(previousCount + 1, _table);
            if (!currentTier.Succeeded)
            {
                return RaceProgressionResult.Failure(previousCount, currentTier.Message);
            }

            _progressionState.RegisterCompletedRace();
            var currentCount = _progressionState.CompletedRaceCount;
            RaceProgressionChanged?.Invoke(new RaceProgressionChangedEvent(previousCount, currentCount));

            var result = new RaceProgressionResult(true, previousCount, currentCount, previousTier.Tier, currentTier.Tier, string.Empty);
            if (result.NewTierReached)
            {
                DifficultyTierChanged?.Invoke(new DifficultyTierChangedEvent(previousTier.Tier, currentTier.Tier));
            }

            return result;
        }

        public bool TryGetCurrentAiConfiguration(out AiDriverConfiguration aiConfiguration, out string message)
        {
            aiConfiguration = null;
            var validation = Validate();
            if (!validation.Succeeded)
            {
                message = validation.Message;
                return false;
            }

            var resolution = ResolveCurrentTier();
            if (!resolution.Succeeded)
            {
                message = resolution.Message;
                return false;
            }

            if (!_aiConfigurationsById.TryGetValue(resolution.Tier.AiConfigurationId, out aiConfiguration) || aiConfiguration == null)
            {
                message = $"AI configuration '{resolution.Tier.AiConfigurationId}' is missing.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
