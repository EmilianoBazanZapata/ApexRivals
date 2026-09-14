using System.Globalization;
using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Pure, testable presentation rules for vehicle telemetry - unit conversion and
    /// short display-text mapping only. Contains no physics or drivetrain calculations;
    /// callers read authoritative runtime values (IVehicleTelemetry) and pass them
    /// straight through.
    /// </summary>
    public static class VehicleTelemetryPresentation
    {
        private const float MetersPerSecondToKilometersPerHour = 3.6f;

        public static float ConvertSpeedToKph(float metersPerSecond)
        {
            return Mathf.Abs(metersPerSecond) * MetersPerSecondToKilometersPerHour;
        }

        public static int RoundSpeedKph(float speedKph)
        {
            return Mathf.RoundToInt(speedKph);
        }

        public static int RoundRpm(float engineRpm)
        {
            return Mathf.RoundToInt(engineRpm);
        }

        public static string FormatSpeedKph(int roundedSpeedKph)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} km/h", roundedSpeedKph);
        }

        public static string FormatRpm(int roundedRpm)
        {
            return roundedRpm.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatGear(int currentGear, bool isReversing)
        {
            return isReversing ? "R" : currentGear.ToString(CultureInfo.InvariantCulture);
        }
    }
}
