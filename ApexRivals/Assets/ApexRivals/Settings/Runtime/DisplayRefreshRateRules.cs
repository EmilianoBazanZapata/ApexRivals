using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Settings.Runtime
{
    /// <summary>
    /// Pure refresh-rate rules shared by the display adapter and settings UI.
    /// </summary>
    public static class DisplayRefreshRateRules
    {
        public static bool IsRefreshRateSelectable(FullScreenMode fullScreenMode)
        {
            return fullScreenMode == FullScreenMode.ExclusiveFullScreen;
        }

        public static int RoundRefreshRate(RefreshRate refreshRate)
        {
            var value = refreshRate.value;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.RoundToInt((float)value));
        }

        /// <summary>
        /// Resolves a rounded refresh rate to the exact ratio exposed by the display.
        /// </summary>
        public static RefreshRate ResolveExactRefreshRate(
            IReadOnlyList<Resolution> displayModes,
            int width,
            int height,
            int roundedRefreshRate)
        {
            if (roundedRefreshRate <= 0)
            {
                return default;
            }

            if (displayModes != null)
            {
                for (var index = 0; index < displayModes.Count; index++)
                {
                    var mode = displayModes[index];
                    if (mode.width == width
                        && mode.height == height
                        && RoundRefreshRate(mode.refreshRateRatio) == roundedRefreshRate)
                    {
                        return mode.refreshRateRatio;
                    }
                }
            }

            return new RefreshRate { numerator = (uint)roundedRefreshRate, denominator = 1 };
        }
    }
}
