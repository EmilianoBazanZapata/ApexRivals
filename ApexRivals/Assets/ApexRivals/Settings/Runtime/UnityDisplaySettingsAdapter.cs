using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Settings.Runtime
{
    public sealed class UnityDisplaySettingsAdapter : IDisplaySettingsAdapter
    {
        public IReadOnlyList<SettingsResolution> SupportedResolutions => CreateSupportedResolutions();

        public SettingsResolution CurrentResolution
        {
            get
            {
                var current = Screen.currentResolution;
                return new SettingsResolution(current.width, current.height, GetRefreshRate(current));
            }
        }

        public SettingsOperationResult Apply(SettingsState settings)
        {
            var supported = SettingsDisplayCatalog.ContainsResolution(SupportedResolutions, settings.Resolution);
            if (!supported && SupportedResolutions.Count > 0)
            {
                return new SettingsOperationResult(SettingsOperationStatus.UnsupportedResolution, "Selected resolution is not supported by this display.");
            }

            Screen.SetResolution(settings.Width, settings.Height, settings.FullScreenMode, CreateRefreshRate(settings.RefreshRate));
            return SettingsOperationResult.Success();
        }

        private static IReadOnlyList<SettingsResolution> CreateSupportedResolutions()
        {
            var resolutions = Screen.resolutions;
            var mapped = new SettingsResolution[resolutions.Length];
            for (var index = 0; index < resolutions.Length; index++)
            {
                var resolution = resolutions[index];
                mapped[index] = new SettingsResolution(resolution.width, resolution.height, GetRefreshRate(resolution));
            }

            return SettingsDisplayCatalog.RemoveDuplicates(mapped);
        }

        private static int GetRefreshRate(Resolution resolution)
        {
            var value = resolution.refreshRateRatio.value;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0;
            }

            return Math.Max(0, Mathf.RoundToInt((float)value));
        }

        private static RefreshRate CreateRefreshRate(int refreshRate)
        {
            return refreshRate > 0
                ? new RefreshRate { numerator = (uint)refreshRate, denominator = 1 }
                : default;
        }
    }
}
