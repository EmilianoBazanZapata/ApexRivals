using ApexRivals.AI.Configuration;

namespace ApexRivals.Progression.Runtime
{
    public interface IRaceDifficultyProvider
    {
        DifficultyTierResolutionResult ResolveCurrentTier();
        bool TryGetCurrentAiConfiguration(out AiDriverConfiguration aiConfiguration, out string message);
    }
}
