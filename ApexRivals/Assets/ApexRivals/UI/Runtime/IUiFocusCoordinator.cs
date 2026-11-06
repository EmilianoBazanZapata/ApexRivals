namespace ApexRivals.UI.Runtime
{
    public interface IUiFocusCoordinator
    {
        void NotifyNavigationInput();
        void NotifyMouseInput();
        void NotifyCancelInput();
    }
}
