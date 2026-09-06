using System;
using System.Collections.Generic;
using ApexRivals.AI.Configuration;

namespace ApexRivals.Progression.Runtime
{
    public enum RaceProgressionConfigurationStatus
    {
        Valid,
        MissingConfiguration,
        MissingInitialTier,
        DuplicateTierId,
        NegativeThreshold,
        UnorderedThreshold,
        MissingAiConfiguration
    }

    public readonly struct RaceProgressionConfigurationResult
    {
        public RaceProgressionConfigurationResult(RaceProgressionConfigurationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public RaceProgressionConfigurationStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == RaceProgressionConfigurationStatus.Valid;

        public static RaceProgressionConfigurationResult Valid()
        {
            return new RaceProgressionConfigurationResult(RaceProgressionConfigurationStatus.Valid, string.Empty);
        }

        public static RaceProgressionConfigurationResult Failure(RaceProgressionConfigurationStatus status, string message)
        {
            return new RaceProgressionConfigurationResult(status, message);
        }
    }

    public readonly struct RaceProgressionTier
    {
        public RaceProgressionTier(string tierId, string displayName, int requiredCompletedRaceCount, string aiConfigurationId)
        {
            TierId = string.IsNullOrWhiteSpace(tierId) ? string.Empty : tierId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? TierId : displayName.Trim();
            RequiredCompletedRaceCount = requiredCompletedRaceCount;
            AiConfigurationId = string.IsNullOrWhiteSpace(aiConfigurationId) ? string.Empty : aiConfigurationId.Trim();
        }

        public string TierId { get; }
        public string DisplayName { get; }
        public int RequiredCompletedRaceCount { get; }
        public string AiConfigurationId { get; }
    }

    public sealed class RaceProgressionTable
    {
        private readonly RaceProgressionTier[] _tiers;

        public RaceProgressionTable(IReadOnlyList<RaceProgressionTier> tiers)
        {
            _tiers = tiers != null ? Copy(tiers) : Array.Empty<RaceProgressionTier>();
        }

        public IReadOnlyList<RaceProgressionTier> Tiers => _tiers;

        public RaceProgressionConfigurationResult Validate()
        {
            if (_tiers.Length == 0)
            {
                return RaceProgressionConfigurationResult.Failure(RaceProgressionConfigurationStatus.MissingConfiguration, "Progression tiers are missing.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var previousThreshold = -1;
            var hasInitialTier = false;
            for (var index = 0; index < _tiers.Length; index++)
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

    public readonly struct DifficultyTierResolutionResult
    {
        public DifficultyTierResolutionResult(bool succeeded, RaceProgressionTier tier, string message)
        {
            Succeeded = succeeded;
            Tier = tier;
            Message = message;
        }

        public bool Succeeded { get; }
        public RaceProgressionTier Tier { get; }
        public string Message { get; }
    }

    public sealed class DifficultyTierResolver
    {
        public DifficultyTierResolutionResult Resolve(int completedRaceCount, RaceProgressionTable table)
        {
            if (completedRaceCount < 0)
            {
                return Failure("Completed race count cannot be negative.");
            }

            if (table == null)
            {
                return Failure("Progression configuration is missing.");
            }

            var validation = table.Validate();
            if (!validation.Succeeded)
            {
                return Failure(validation.Message);
            }

            var tiers = table.Tiers;
            var selected = tiers[0];
            for (var index = 0; index < tiers.Count; index++)
            {
                var tier = tiers[index];
                if (completedRaceCount < tier.RequiredCompletedRaceCount)
                {
                    break;
                }

                selected = tier;
            }

            return new DifficultyTierResolutionResult(true, selected, string.Empty);
        }

        private static DifficultyTierResolutionResult Failure(string message)
        {
            return new DifficultyTierResolutionResult(false, default, message);
        }
    }

    public readonly struct RaceProgressionChangedEvent
    {
        public RaceProgressionChangedEvent(int previousCompletedRaceCount, int completedRaceCount)
        {
            PreviousCompletedRaceCount = previousCompletedRaceCount;
            CompletedRaceCount = completedRaceCount;
        }

        public int PreviousCompletedRaceCount { get; }
        public int CompletedRaceCount { get; }
    }

    public readonly struct DifficultyTierChangedEvent
    {
        public DifficultyTierChangedEvent(RaceProgressionTier previousTier, RaceProgressionTier currentTier)
        {
            PreviousTier = previousTier;
            CurrentTier = currentTier;
        }

        public RaceProgressionTier PreviousTier { get; }
        public RaceProgressionTier CurrentTier { get; }
    }

    public readonly struct RaceProgressionResult
    {
        public RaceProgressionResult(
            bool succeeded,
            int previousCompletedRaceCount,
            int completedRaceCount,
            RaceProgressionTier previousTier,
            RaceProgressionTier currentTier,
            string message)
        {
            Succeeded = succeeded;
            PreviousCompletedRaceCount = previousCompletedRaceCount;
            CompletedRaceCount = completedRaceCount;
            PreviousTier = previousTier;
            CurrentTier = currentTier;
            Message = message;
        }

        public bool Succeeded { get; }
        public int PreviousCompletedRaceCount { get; }
        public int CompletedRaceCount { get; }
        public RaceProgressionTier PreviousTier { get; }
        public RaceProgressionTier CurrentTier { get; }
        public string Message { get; }
        public bool NewTierReached => Succeeded && !string.Equals(PreviousTier.TierId, CurrentTier.TierId, StringComparison.Ordinal);

        public static RaceProgressionResult Failure(int completedRaceCount, string message)
        {
            return new RaceProgressionResult(false, completedRaceCount, completedRaceCount, default, default, message);
        }
    }

    public interface IRaceDifficultyProvider
    {
        DifficultyTierResolutionResult ResolveCurrentTier();
        bool TryGetCurrentAiConfiguration(out AiDriverConfiguration aiConfiguration, out string message);
    }

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
