namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct SaveDeserializationResult
    {
        public SaveDeserializationResult(bool succeeded, PlayerProfileSaveData saveData, string error, bool isNewerSchema = false)
        {
            Succeeded = succeeded;
            SaveData = saveData;
            Error = error;
            IsNewerSchema = isNewerSchema;
        }

        public bool Succeeded { get; }
        public PlayerProfileSaveData SaveData { get; }
        public string Error { get; }

        /// <summary>The payload was written by a newer build; it must be left untouched, never replaced.</summary>
        public bool IsNewerSchema { get; }
    }
}
