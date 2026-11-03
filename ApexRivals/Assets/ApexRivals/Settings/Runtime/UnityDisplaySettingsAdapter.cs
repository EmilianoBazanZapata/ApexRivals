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
                return new SettingsResolution(current.width, current.height);
            }
        }

        public SettingsOperationResult Apply(SettingsState settings)
        {
            var resolutions = CreateSupportedResolutions(Screen.resolutions);
            var supported = SettingsDisplayCatalog.ContainsResolution(resolutions, settings.Resolution);
            if (!supported && resolutions.Count > 0)
            {
                return new SettingsOperationResult(SettingsOperationStatus.UnsupportedResolution, "Selected resolution is not supported by this display.");
            }

            Screen.SetResolution(settings.Width, settings.Height, settings.FullScreenMode);
            return SettingsOperationResult.Success();
        }

        /// <summary>One entry per size: the display picks its own refresh rate.</summary>
        private static IReadOnlyList<SettingsResolution> CreateSupportedResolutions(Resolution[] resolutions)
        {
            var mapped = new SettingsResolution[resolutions.Length];
            for (var index = 0; index < resolutions.Length; index++)
            {
                mapped[index] = new SettingsResolution(resolutions[index].width, resolutions[index].height);
            }

            return SettingsDisplayCatalog.RemoveDuplicates(mapped);
        }
    }
}
