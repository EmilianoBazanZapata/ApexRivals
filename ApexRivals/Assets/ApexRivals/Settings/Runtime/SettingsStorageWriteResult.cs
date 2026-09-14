namespace ApexRivals.Settings.Runtime
{
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
