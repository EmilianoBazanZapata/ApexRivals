namespace ApexRivals.Career.Runtime
{
    public readonly struct CareerSummary
    {
        public CareerSummary(
            int completionPercent,
            int achievedMilestones,
            int totalMilestones,
            CareerObjective objective,
            int ownedVehicles,
            int purchasedVehicles,
            int totalVehicles,
            int unlockedTracks,
            int totalTracks,
            CareerFinaleStatus finale)
        {
            CompletionPercent = completionPercent;
            AchievedMilestones = achievedMilestones;
            TotalMilestones = totalMilestones;
            Objective = objective;
            OwnedVehicles = ownedVehicles;
            PurchasedVehicles = purchasedVehicles;
            TotalVehicles = totalVehicles;
            UnlockedTracks = unlockedTracks;
            TotalTracks = totalTracks;
            Finale = finale;
        }

        public int CompletionPercent { get; }
        public int AchievedMilestones { get; }
        public int TotalMilestones { get; }
        public CareerObjective Objective { get; }
        public int OwnedVehicles { get; }
        public int PurchasedVehicles { get; }
        public int TotalVehicles { get; }
        public int UnlockedTracks { get; }
        public int TotalTracks { get; }
        public CareerFinaleStatus Finale { get; }
        public bool IsComplete => Finale.State == CareerFinaleState.Won;
    }
}
