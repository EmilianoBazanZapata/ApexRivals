namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct SaveDataValidationResult
    {
        public SaveDataValidationResult(bool isValid, PlayerProfileSaveData saveData, string message)
        {
            IsValid = isValid;
            SaveData = saveData;
            Message = message;
        }

        public bool IsValid { get; }
        public PlayerProfileSaveData SaveData { get; }
        public string Message { get; }
    }
}
