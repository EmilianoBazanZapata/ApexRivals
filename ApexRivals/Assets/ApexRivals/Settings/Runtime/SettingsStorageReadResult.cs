namespace ApexRivals.Settings.Runtime
{
    public readonly struct SettingsStorageReadResult
    {
        public SettingsStorageReadResult(SettingsStorageReadStatus status, SettingsState settings, string message)
        {
            Status = status;
            Settings = settings;
            Message = message ?? string.Empty;
        }

        public SettingsStorageReadStatus Status { get; }
        public SettingsState Settings { get; }
        public string Message { get; }
        public bool Succeeded => Status == SettingsStorageReadStatus.Loaded || Status == SettingsStorageReadStatus.Invalid;
    }
}
