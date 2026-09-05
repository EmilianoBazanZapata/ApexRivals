namespace ApexRivals.Settings.Runtime
{
    public enum SettingsStorageReadStatus
    {
        Loaded,
        Missing,
        Invalid,
        Failed
    }

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

    public readonly struct SettingsStorageWriteResult
    {
        public SettingsStorageWriteResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }
}
