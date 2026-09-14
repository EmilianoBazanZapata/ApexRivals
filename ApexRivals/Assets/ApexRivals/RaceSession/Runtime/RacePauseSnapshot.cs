namespace ApexRivals.RaceSession.Runtime
{
    public readonly struct RacePauseSnapshot
    {
        public RacePauseSnapshot(bool canResume, bool canRetry, bool canReturnToMainMenu)
        {
            CanResume = canResume;
            CanRetry = canRetry;
            CanReturnToMainMenu = canReturnToMainMenu;
        }

        public bool CanResume { get; }
        public bool CanRetry { get; }
        public bool CanReturnToMainMenu { get; }
    }
}
