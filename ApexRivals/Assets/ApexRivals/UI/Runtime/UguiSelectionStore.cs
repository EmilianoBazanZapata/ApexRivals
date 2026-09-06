using UnityEngine.EventSystems;

namespace ApexRivals.UI.Runtime
{
    public sealed class UguiSelectionStore : IUiSelectionStore
    {
        private string _previousSelectionId = string.Empty;

        public string CurrentSelectionId { get; private set; } = string.Empty;

        public void Select(string selectionId)
        {
            if (EventSystem.current != null)
            {
                _previousSelectionId = CurrentSelectionId;
            }

            CurrentSelectionId = selectionId ?? string.Empty;
        }

        public void RestorePreviousSelection()
        {
            CurrentSelectionId = _previousSelectionId;
        }
    }
}
