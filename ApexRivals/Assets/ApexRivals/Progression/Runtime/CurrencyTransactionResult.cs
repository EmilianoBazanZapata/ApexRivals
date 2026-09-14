namespace ApexRivals.Progression.Runtime
{
    public readonly struct CurrencyTransactionResult
    {
        public CurrencyTransactionResult(CurrencyTransactionStatus status, int currency)
        {
            Status = status;
            Currency = currency;
        }

        public CurrencyTransactionStatus Status { get; }
        public int Currency { get; }
        public bool Succeeded => Status == CurrencyTransactionStatus.Succeeded;
    }
}
