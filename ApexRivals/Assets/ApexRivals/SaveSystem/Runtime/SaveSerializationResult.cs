namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct SaveSerializationResult
    {
        public SaveSerializationResult(bool succeeded, string json, string error)
        {
            Succeeded = succeeded;
            Json = json;
            Error = error;
        }

        public bool Succeeded { get; }
        public string Json { get; }
        public string Error { get; }
    }
}
