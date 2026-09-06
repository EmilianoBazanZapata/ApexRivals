namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct PlayerProfileSaveResult
    {
        public PlayerProfileSaveResult(PlayerProfileSaveStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public PlayerProfileSaveStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == PlayerProfileSaveStatus.Saved || Status == PlayerProfileSaveStatus.Deleted;
    }
}
