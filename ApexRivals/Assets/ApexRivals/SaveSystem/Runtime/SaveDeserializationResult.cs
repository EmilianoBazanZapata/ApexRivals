namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct SaveDeserializationResult
    {
        public SaveDeserializationResult(bool succeeded, PlayerProfileSaveData saveData, string error)
        {
            Succeeded = succeeded;
            SaveData = saveData;
            Error = error;
        }

        public bool Succeeded { get; }
        public PlayerProfileSaveData SaveData { get; }
        public string Error { get; }
    }
}
