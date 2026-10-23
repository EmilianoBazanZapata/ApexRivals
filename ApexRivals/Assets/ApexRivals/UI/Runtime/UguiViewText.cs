using System.Globalization;
using ApexRivals.Garage.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    internal static class UguiViewText
    {
        public static void Set(Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            var resolvedValue = value ?? string.Empty;
            if (text.text != resolvedValue)
            {
                text.text = resolvedValue;
            }
        }

        public static string FormatCurrency(int currency)
        {
            return "$ " + currency.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string FormatEngineBenefit(UpgradeStatModifier modifier)
        {
            var percent = (modifier.MaximumSpeedMultiplier - 1f) * 100f;
            return string.Format(CultureInfo.InvariantCulture, "+{0:0}% TOP SPEED", percent);
        }

        public static string FormatHandlingBenefit(UpgradeStatModifier modifier)
        {
            var percent = System.Math.Max(modifier.NormalGripBonus, modifier.DriftGripBonus) * 100f;
            return string.Format(CultureInfo.InvariantCulture, "+{0:0}% GRIP", percent);
        }

        public static string FormatPurchaseStatus(PresentationStatus status)
        {
            return status switch
            {
                PresentationStatus.Succeeded => "UPGRADE PURCHASED",
                PresentationStatus.InsufficientCurrency => "NOT ENOUGH CURRENCY",
                PresentationStatus.MaximumLevelReached => "MAXIMUM LEVEL REACHED",
                PresentationStatus.Invalid => string.Empty,
                _ => string.Empty
            };
        }

        public static string FormatStats(VehiclePerformanceStats stats)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Acceleration {0:0.0}\nTop Speed {1:0.0}\nSteering {2:0.0}\nGrip {3:0.00}/{4:0.00}",
                stats.Acceleration,
                stats.TopSpeed,
                stats.Steering,
                stats.Handling,
                stats.DriftHandling);
        }

        public const string StatLabels = "Acceleration\nTop Speed\nSteering\nGrip";

        /// <summary>Values column matching <see cref="StatLabels"/> line by line.</summary>
        public static string FormatStatValues(VehiclePerformanceStats stats)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0}\n{1:0.0}\n{2:0.0}\n{3:0.00} / {4:0.00}",
                stats.Acceleration,
                stats.TopSpeed,
                stats.Steering,
                stats.Handling,
                stats.DriftHandling);
        }

        public static string FormatVehicleCounter(int index, int count)
        {
            return count <= 0 ? "- / -" : string.Format(CultureInfo.InvariantCulture, "{0} / {1}", index + 1, count);
        }

        public static string FormatTrackLength(float meters)
        {
            return meters <= 0f
                ? "-"
                : string.Format(CultureInfo.InvariantCulture, "{0:0.0} KM", meters / 1000f);
        }

        public static string FormatTime(float seconds)
        {
            var minutes = (int)(seconds / 60f);
            var remainder = seconds - minutes * 60f;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00.000}", minutes, remainder);
        }
    }
}
