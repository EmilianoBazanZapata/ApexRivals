using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Settings.Runtime
{
    public sealed class UnityDisplaySettingsAdapter : IDisplaySettingsAdapter
    {
        public IReadOnlyList<SettingsResolution> SupportedResolutions => CreateSupportedResolutions(Screen.resolutions);

        public SettingsResolution CurrentResolution
        {
            get
            {
                var current = Screen.currentResolution;
                return new SettingsResolution(
                    current.width,
                    current.height,
                    DisplayRefreshRateRules.RoundRefreshRate(current.refreshRateRatio));
            }
        }

        public SettingsOperationResult Apply(SettingsState settings)
        {
            var nativeResolutions = Screen.resolutions;
            var resolutions = CreateSupportedResolutions(nativeResolutions);
            var supported = SettingsDisplayCatalog.ContainsResolution(resolutions, settings.Resolution);
            if (!supported && resolutions.Count > 0)
            {
                return new SettingsOperationResult(SettingsOperationStatus.UnsupportedResolution, "Selected resolution is not supported by this display.");
            }

            var refreshRate = DisplayRefreshRateRules.ResolveExactRefreshRate(
                nativeResolutions,
                settings.Width,
                settings.Height,
                settings.RefreshRate);
            Screen.SetResolution(settings.Width, settings.Height, settings.FullScreenMode, refreshRate);
            return SettingsOperationResult.Success();
        }

        private static IReadOnlyList<SettingsResolution> CreateSupportedResolutions(Resolution[] resolutions)
        {
            var mapped = new SettingsResolution[resolutions.Length];
            for (var index = 0; index < resolutions.Length; index++)
            {
                var resolution = resolutions[index];
                mapped[index] = new SettingsResolution(
                    resolution.width,
                    resolution.height,
                    DisplayRefreshRateRules.RoundRefreshRate(resolution.refreshRateRatio));
            }

            return SettingsDisplayCatalog.RemoveDuplicates(mapped);
        }
    }
}
