namespace ApexRivals.Settings.Runtime
{
    public enum SettingsOperationStatus
    {
        Succeeded,
        DefaultsLoaded,
        LoadedWithInvalidValues,
        LoadedDefaultsAfterFailure,
        UnsupportedResolution,
        DisplayApplyFailed,
        StorageFailed
    }
}
