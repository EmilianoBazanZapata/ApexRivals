using System.Globalization;
using ApexRivals.Vehicle.Runtime;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    internal static class UguiViewText
    {
        public static void Set(Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        public static string FormatStats(ArcadeVehicleStats stats)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Acceleration {0:0.0}\nTop Speed {1:0.0}\nSteering {2:0.0}\nGrip {3:0.00}/{4:0.00}",
                stats.ForwardAcceleration,
                stats.MaximumForwardSpeed,
                stats.SteeringStrength,
                stats.NormalGrip,
                stats.DriftGrip);
        }

        public static string FormatTime(float seconds)
        {
            var minutes = (int)(seconds / 60f);
            var remainder = seconds - minutes * 60f;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00.000}", minutes, remainder);
        }
    }
}
