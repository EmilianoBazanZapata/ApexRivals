using System.Collections.Generic;

namespace ApexRivals.Settings.Runtime
{
    public static class SettingsDisplayCatalog
    {
        public static IReadOnlyList<SettingsResolution> RemoveDuplicates(IEnumerable<SettingsResolution> resolutions)
        {
            var unique = new List<SettingsResolution>();
            if (resolutions == null)
            {
                return unique;
            }

            foreach (var resolution in resolutions)
            {
                if (!resolution.IsValid || ContainsResolution(unique, resolution))
                {
                    continue;
                }

                unique.Add(resolution);
            }

            return unique;
        }

        public static bool ContainsResolution(IReadOnlyList<SettingsResolution> resolutions, SettingsResolution resolution)
        {
            if (resolutions == null || !resolution.IsValid)
            {
                return false;
            }

            for (var index = 0; index < resolutions.Count; index++)
            {
                if (resolutions[index] == resolution)
                {
                    return true;
                }
            }

            return false;
        }

        public static SettingsResolution ResolveOrFallback(
            IReadOnlyList<SettingsResolution> supportedResolutions,
            SettingsResolution requested,
            SettingsResolution current,
            SettingsResolution defaultResolution)
        {
            if (ContainsResolution(supportedResolutions, requested))
            {
                return requested;
            }

            if (ContainsResolution(supportedResolutions, current))
            {
                return current;
            }

            if (ContainsResolution(supportedResolutions, defaultResolution))
            {
                return defaultResolution;
            }

            return supportedResolutions != null && supportedResolutions.Count > 0
                ? supportedResolutions[0]
                : defaultResolution;
        }
    }
}
