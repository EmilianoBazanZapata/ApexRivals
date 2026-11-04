namespace ApexRivals.UI.Runtime
{
    public interface IPresentationScreenNavigator
    {
        PresentationScreenState CurrentScreen { get; }
        void Show(PresentationScreenState screenState);
    }
}
