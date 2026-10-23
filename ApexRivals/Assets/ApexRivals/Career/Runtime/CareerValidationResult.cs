namespace ApexRivals.Career.Runtime
{
    public readonly struct CareerValidationResult
    {
        private CareerValidationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static CareerValidationResult Valid() => new CareerValidationResult(true, string.Empty);

        public static CareerValidationResult Failure(string message) => new CareerValidationResult(false, message);
    }
}
