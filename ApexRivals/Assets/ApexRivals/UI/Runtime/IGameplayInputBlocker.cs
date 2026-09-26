namespace ApexRivals.UI.Runtime
{
    public interface IGameplayInputBlocker
    {
        bool GameplayInputBlocked { get; }
        void SetGameplayInputBlocked(bool blocked);
    }
}
