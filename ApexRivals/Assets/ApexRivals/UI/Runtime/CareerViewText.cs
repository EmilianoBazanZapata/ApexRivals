using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ApexRivals.Career.Runtime;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Player-facing career wording. Presenters build view models with it; rules stay in the career.</summary>
    public static class CareerViewText
    {
        public static CareerPanelViewModel CreatePanel(CareerSummary summary, string announcement = "")
        {
            var objective = summary.Objective;
            return new CareerPanelViewModel(
                summary.CompletionPercent,
                string.Format(CultureInfo.InvariantCulture, "CAREER {0}%", summary.CompletionPercent),
                summary.IsComplete ? "CAREER COMPLETE" : objective.HasObjective ? "NEXT: " + Upper(objective.Title) : string.Empty,
                summary.IsComplete ? "You are the Apex Rivals champion. Keep racing any circuit." : FormatObjectiveDetail(objective),
                FormatCollection(summary),
                FormatFinale(summary.Finale),
                announcement,
                summary.IsComplete);
        }

        public static string FormatObjectiveDetail(CareerObjective objective)
        {
            if (!objective.HasObjective)
            {
                return string.Empty;
            }

            var detail = objective.Hint;
            if (!string.IsNullOrEmpty(objective.ProgressText))
            {
                detail += "  (" + objective.ProgressText + ")";
            }

            if (objective.CurrencyReward > 0)
            {
                detail += "  Bonus " + UguiViewText.FormatCurrency(objective.CurrencyReward);
            }

            return detail;
        }

        public static string FormatCollection(CareerSummary summary)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "CARS {0}/{1} ({2} BOUGHT)   CIRCUITS {3}/{4}   MILESTONES {5}/{6}",
                summary.OwnedVehicles,
                summary.TotalVehicles,
                summary.PurchasedVehicles,
                summary.UnlockedTracks,
                summary.TotalTracks,
                summary.AchievedMilestones,
                summary.TotalMilestones);
        }

        public static string FormatFinale(CareerFinaleStatus finale)
        {
            var title = Upper(finale.Title);
            return finale.State switch
            {
                CareerFinaleState.Won => title + ": WON",
                CareerFinaleState.Available => title + ": READY IN RACE SELECT",
                _ => string.Format(CultureInfo.InvariantCulture, "{0}: LOCKED ({1}/{2})", title, finale.MetRequirements, finale.TotalRequirements)
            };
        }

        public static string FormatAwards(IReadOnlyList<CareerMilestoneAward> awards)
        {
            if (awards == null || awards.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < awards.Count; index++)
            {
                if (index > 0) builder.Append('\n');
                builder.Append("OBJECTIVE COMPLETE: ").Append(Upper(awards[index].Title));
                if (awards[index].CreditedReward > 0)
                {
                    builder.Append("  +").Append(UguiViewText.FormatCurrency(awards[index].CreditedReward));
                }
            }

            return builder.ToString();
        }

        /// <summary>One line under a car: owned, or its price and what still blocks buying it.</summary>
        public static string FormatVehicleStatus(CareerVehicleStatus status, int currency)
        {
            if (status.Owned)
            {
                return status.IsStarter ? "OWNED - STARTER CAR" : "OWNED";
            }

            if (!status.ForSale)
            {
                return "NOT FOR SALE";
            }

            var price = UguiViewText.FormatCurrency(status.Price);
            if (!status.PrerequisiteMet)
            {
                return "LOCKED - " + price + " - REQUIRES: " + Upper(status.PrerequisiteTitle);
            }

            return status.CanAfford
                ? "FOR SALE - " + price
                : "FOR SALE - " + price + " - NEED " + UguiViewText.FormatCurrency(status.Price - currency) + " MORE";
        }

        public static string FormatPurchase(CareerVehiclePurchaseResult result, string vehicleName)
        {
            return result.Status switch
            {
                CareerVehiclePurchaseStatus.Purchased => Upper(vehicleName) + " PURCHASED",
                CareerVehiclePurchaseStatus.InsufficientFunds => "NOT ENOUGH CURRENCY",
                CareerVehiclePurchaseStatus.PrerequisiteNotMet => Upper(result.Message),
                CareerVehiclePurchaseStatus.SaveFailed => "PURCHASE CANCELLED: COULD NOT SAVE",
                CareerVehiclePurchaseStatus.AlreadyOwned => "ALREADY OWNED",
                _ => "PURCHASE FAILED"
            };
        }

        /// <summary>Title line for the results screen career block.</summary>
        public static string FormatOutcomeTitle(CareerRaceOutcome outcome)
        {
            if (outcome == null || outcome.Status == CareerRaceOutcomeStatus.None)
            {
                return string.Empty;
            }

            if (outcome.CareerCompleted)
            {
                return "CHAMPION! CAREER COMPLETE - 100%";
            }

            if (outcome.IsFinale)
            {
                return outcome.Won ? "FINAL WON" : "FINAL LOST - RETRY TO TAKE THE TITLE";
            }

            return string.Format(CultureInfo.InvariantCulture, "CAREER {0}%", outcome.Summary.CompletionPercent);
        }

        /// <summary>Body lines: objectives completed and bonuses, unlocks, cars for sale, next objective.</summary>
        public static string FormatOutcomeBody(CareerRaceOutcome outcome)
        {
            if (outcome == null || outcome.Status == CareerRaceOutcomeStatus.None)
            {
                return string.Empty;
            }

            if (outcome.Status == CareerRaceOutcomeStatus.NotCounted)
            {
                return "This race does not count for the career.";
            }

            var builder = new StringBuilder();
            var awards = FormatAwards(outcome.Awards);
            if (!string.IsNullOrEmpty(awards))
            {
                builder.AppendLine(awards);
            }

            foreach (var track in outcome.NewlyUnlockedTracks)
            {
                builder.Append("NEW CIRCUIT UNLOCKED: ").AppendLine(Upper(track));
            }

            foreach (var vehicle in outcome.NewlyPurchasableVehicles)
            {
                builder.Append("NEW CAR FOR SALE: ").AppendLine(Upper(vehicle));
            }

            if (outcome.CompletionAfter > outcome.CompletionBefore)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "COMPLETION {0}% > {1}%", outcome.CompletionBefore, outcome.CompletionAfter).AppendLine();
            }

            var summary = outcome.Summary;
            if (summary.IsComplete)
            {
                builder.Append(outcome.CareerCompleted ? "Final bonus paid. Every circuit stays open: keep racing!" : "Career complete. Keep racing!");
            }
            else if (summary.Objective.HasObjective)
            {
                builder.Append("NEXT: ").Append(Upper(summary.Objective.Title));
            }

            return builder.ToString().TrimEnd();
        }

        private static string Upper(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToUpperInvariant();
        }
    }
}
