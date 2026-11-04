using System.Globalization;
using ApexRivals.Garage.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    internal static class UguiViewText
    {
        /// <summary>Matches an envelope <see cref="UnityEngine.UI.AspectRatioFitter"/> to the sprite so screenshots crop instead of stretching.</summary>
        public static void FitAspect(Image image, UnityEngine.Sprite sprite, bool automatic)
        {
            if (!automatic || image == null || sprite == null || sprite.rect.height <= 0f)
            {
                return;
            }

            var fitter = image.GetComponent<AspectRatioFitter>();
            var aspect = sprite.rect.width / sprite.rect.height;
            if (fitter != null && !UnityEngine.Mathf.Approximately(fitter.aspectRatio, aspect))
            {
                fitter.aspectRatio = aspect;
            }
        }

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

        /// <summary>Invalid Inspector templates fall back to the original format without interrupting the UI.</summary>
        public static string FormatTemplate(string template, string fallback, params object[] values)
        {
            try { return string.Format(CultureInfo.InvariantCulture, template ?? string.Empty, values); }
            catch (System.FormatException) { return string.Format(CultureInfo.InvariantCulture, fallback, values); }
        }

        public static string FormatCurrency(int currency)
        {
            return "$ " + currency.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string FormatEngineBenefit(UpgradeStatModifier modifier, string format = "+{0:0}% TOP SPEED")
        {
            var percent = (modifier.MaximumSpeedMultiplier - 1f) * 100f;
            return FormatTemplate(format, "+{0:0}% TOP SPEED", percent);
        }

        public static string FormatHandlingBenefit(UpgradeStatModifier modifier, string format = "+{0:0}% GRIP")
        {
            var percent = System.Math.Max(modifier.NormalGripBonus, modifier.DriftGripBonus) * 100f;
            return FormatTemplate(format, "+{0:0}% GRIP", percent);
        }

        public static string FormatPurchaseStatus(PresentationStatus status, string purchased = "UPGRADE PURCHASED", string insufficient = "NOT ENOUGH CURRENCY", string maximum = "MAXIMUM LEVEL REACHED")
        {
            return status switch
            {
                PresentationStatus.Succeeded => purchased,
                PresentationStatus.InsufficientCurrency => insufficient,
                PresentationStatus.MaximumLevelReached => maximum,
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
