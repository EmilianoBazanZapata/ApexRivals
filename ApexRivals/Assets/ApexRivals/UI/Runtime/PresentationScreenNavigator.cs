namespace ApexRivals.UI.Runtime
{
    public sealed class PresentationScreenNavigator : IPresentationScreenNavigator
    {
        public PresentationScreenNavigator(PresentationScreenState initialScreen = PresentationScreenState.Main)
        {
            CurrentScreen = initialScreen;
        }

        public PresentationScreenState CurrentScreen { get; private set; }

        public void Show(PresentationScreenState screenState)
        {
            CurrentScreen = screenState;
        }
    }
}
