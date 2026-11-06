namespace ApexRivals.UI.Runtime
{
    public readonly struct PauseViewModel
    {
        public PauseViewModel(bool isPaused, bool isTransitioning, bool canResume, bool canRetry, bool canReturnToMainMenu, PresentationFailure failure, string messageKey)
        {
            IsPaused = isPaused;
            IsTransitioning = isTransitioning;
            CanResume = canResume;
            CanRetry = canRetry;
            CanReturnToMainMenu = canReturnToMainMenu;
            Failure = failure;
            MessageKey = messageKey;
        }

        public bool IsPaused { get; }
        public bool IsTransitioning { get; }
        public bool CanResume { get; }
        public bool CanRetry { get; }
        public bool CanReturnToMainMenu { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }
}
