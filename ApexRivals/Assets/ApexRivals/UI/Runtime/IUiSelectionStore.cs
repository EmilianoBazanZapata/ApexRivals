namespace ApexRivals.UI.Runtime
{
    public interface IUiSelectionStore
    {
        string CurrentSelectionId { get; }
        void Select(string selectionId);
        void RestorePreviousSelection();
    }
}
