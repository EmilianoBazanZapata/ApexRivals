using System.Collections.Generic;

namespace ApexRivals.Settings.Runtime
{
    public interface IDisplaySettingsAdapter
    {
        IReadOnlyList<SettingsResolution> SupportedResolutions { get; }
        SettingsResolution CurrentResolution { get; }
        SettingsOperationResult Apply(SettingsState settings);
    }
}
