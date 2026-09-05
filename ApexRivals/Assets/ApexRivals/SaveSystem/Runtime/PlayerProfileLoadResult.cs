namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct PlayerProfileLoadResult
    {
        public PlayerProfileLoadResult(PlayerProfileLoadStatus status, PlayerProfileSaveData saveData, string message)
        {
            Status = status;
            SaveData = saveData;
            Message = message;
        }

        public PlayerProfileLoadStatus Status { get; }
        public PlayerProfileSaveData SaveData { get; }
        public string Message { get; }
        public bool Succeeded => Status != PlayerProfileLoadStatus.Failed;
    }
}
