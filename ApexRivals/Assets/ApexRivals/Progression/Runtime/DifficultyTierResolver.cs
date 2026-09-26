namespace ApexRivals.Progression.Runtime
{
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
}
