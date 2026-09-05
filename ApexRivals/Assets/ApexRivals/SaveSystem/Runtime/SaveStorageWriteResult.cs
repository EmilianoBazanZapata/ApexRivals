namespace ApexRivals.SaveSystem.Runtime
{
    public readonly struct SaveStorageWriteResult
    {
        public SaveStorageWriteResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }
        public string Error { get; }
    }
}
