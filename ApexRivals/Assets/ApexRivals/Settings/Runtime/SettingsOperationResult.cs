namespace ApexRivals.Settings.Runtime
{
    public readonly struct SettingsOperationResult
    {
        public SettingsOperationResult(SettingsOperationStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public SettingsOperationStatus Status { get; }
        public string Message { get; }

        public bool Succeeded =>
            Status == SettingsOperationStatus.Succeeded
            || Status == SettingsOperationStatus.DefaultsLoaded
            || Status == SettingsOperationStatus.LoadedWithInvalidValues
            || Status == SettingsOperationStatus.LoadedDefaultsAfterFailure;

        public static SettingsOperationResult Success()
        {
            return new SettingsOperationResult(SettingsOperationStatus.Succeeded, string.Empty);
        }
    }
}
