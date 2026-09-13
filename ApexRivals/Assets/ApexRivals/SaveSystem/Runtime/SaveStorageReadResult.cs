namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct SaveStorageReadResult
    {
        public SaveStorageReadResult(bool succeeded, string payload, string error)
        {
            Succeeded = succeeded;
            Payload = payload;
            Error = error;
        }

        public bool Succeeded { get; }
        public string Payload { get; }
        public string Error { get; }
    }
}
