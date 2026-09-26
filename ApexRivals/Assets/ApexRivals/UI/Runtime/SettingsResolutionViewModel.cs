using ApexRivals.Settings.Runtime;

namespace ApexRivals.UI.Runtime
{
    public readonly struct SettingsResolutionViewModel
    {
        public SettingsResolutionViewModel(SettingsResolution resolution, bool isSelected)
        {
            Resolution = resolution;
            IsSelected = isSelected;
        }

        public SettingsResolution Resolution { get; }
        public int Width => Resolution.Width;
        public int Height => Resolution.Height;
        public int RefreshRate => Resolution.RefreshRate;
        public bool IsSelected { get; }
    }
}
