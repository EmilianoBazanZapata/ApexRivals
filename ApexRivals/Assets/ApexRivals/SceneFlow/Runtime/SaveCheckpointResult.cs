namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct SaveCheckpointResult
    {
        public SaveCheckpointResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static SaveCheckpointResult Success()
        {
            return new SaveCheckpointResult(true, string.Empty);
        }

        public static SaveCheckpointResult Failure(string message)
        {
            return new SaveCheckpointResult(false, message);
        }
    }
}
