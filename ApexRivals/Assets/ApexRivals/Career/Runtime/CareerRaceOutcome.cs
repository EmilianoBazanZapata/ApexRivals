using System;
using System.Collections.Generic;

namespace ApexRivals.Career.Runtime
{
    /// <summary>What one finished race changed in the career, for the results screen.</summary>
    public sealed class CareerRaceOutcome
    {
        public static readonly CareerRaceOutcome None = new CareerRaceOutcome(CareerRaceOutcomeStatus.None, string.Empty, false, false, null, null, null, 0, 0, false, default);

        public CareerRaceOutcome(
            CareerRaceOutcomeStatus status,
            string trackId,
            bool isFinale,
            bool won,
            IReadOnlyList<CareerMilestoneAward> awards,
            IReadOnlyList<string> newlyUnlockedTracks,
            IReadOnlyList<string> newlyPurchasableVehicles,
            int completionBefore,
            int completionAfter,
            bool careerCompleted,
            CareerSummary summary)
        {
            Status = status;
            TrackId = trackId ?? string.Empty;
            IsFinale = isFinale;
            Won = won;
            Awards = awards ?? Array.Empty<CareerMilestoneAward>();
            NewlyUnlockedTracks = newlyUnlockedTracks ?? Array.Empty<string>();
            NewlyPurchasableVehicles = newlyPurchasableVehicles ?? Array.Empty<string>();
            CompletionBefore = completionBefore;
            CompletionAfter = completionAfter;
            CareerCompleted = careerCompleted;
            Summary = summary;

            var bonus = 0;
            for (var index = 0; index < Awards.Count; index++)
            {
                bonus += Awards[index].CreditedReward;
            }

            BonusCurrency = bonus;
        }

        public CareerRaceOutcomeStatus Status { get; }
        public string TrackId { get; }
        public bool IsFinale { get; }
        public bool Won { get; }
        public IReadOnlyList<CareerMilestoneAward> Awards { get; }

        /// <summary>Display names of circuits this race opened.</summary>
        public IReadOnlyList<string> NewlyUnlockedTracks { get; }

        /// <summary>Display names of cars whose prerequisite this race met.</summary>
        public IReadOnlyList<string> NewlyPurchasableVehicles { get; }
        public int BonusCurrency { get; }
        public int CompletionBefore { get; }
        public int CompletionAfter { get; }

        /// <summary>True only for the race that won the final for the first time.</summary>
        public bool CareerCompleted { get; }

        /// <summary>Career state after the race (objective, counts, final status).</summary>
        public CareerSummary Summary { get; }
        public bool Recorded => Status == CareerRaceOutcomeStatus.Recorded;
    }
}
