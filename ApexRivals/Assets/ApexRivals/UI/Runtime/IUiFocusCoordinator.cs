namespace ApexRivals.UI.Runtime
{
    public interface IUiFocusCoordinator
    {
        /// <summary>True while the player is driving (no menu over the race), so the cursor is captured.</summary>
        bool IsGameplayActive { get; }

        void NotifyNavigationInput();
        void NotifyMouseInput();
        void NotifyCancelInput();
    }
}
