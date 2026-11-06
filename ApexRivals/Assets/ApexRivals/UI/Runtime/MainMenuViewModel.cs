namespace ApexRivals.UI.Runtime
{
    public readonly struct MainMenuViewModel
    {
        public MainMenuViewModel(
            MainMenuStartTarget startTarget,
            bool isVehicleSelectionRequired,
            bool canStartOrContinue,
            bool canOpenGarage,
            bool canOpenSettings,
            bool canQuit,
            bool isTransitioning,
            PresentationFailure failure,
            string messageKey)
        {
            StartTarget = startTarget;
            IsVehicleSelectionRequired = isVehicleSelectionRequired;
            CanStartOrContinue = canStartOrContinue;
            CanOpenGarage = canOpenGarage;
            CanOpenSettings = canOpenSettings;
            CanQuit = canQuit;
            IsTransitioning = isTransitioning;
            Failure = failure;
            MessageKey = messageKey;
        }

        public MainMenuStartTarget StartTarget { get; }
        public bool IsVehicleSelectionRequired { get; }
        public bool CanStartOrContinue { get; }
        public bool CanOpenGarage { get; }
        public bool CanOpenSettings { get; }
        public bool CanQuit { get; }
        public bool IsTransitioning { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }
}
