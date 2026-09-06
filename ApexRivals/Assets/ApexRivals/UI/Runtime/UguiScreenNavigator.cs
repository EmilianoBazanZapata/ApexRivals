using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class UguiScreenNavigator : MonoBehaviour, IPresentationScreenNavigator
    {
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject vehicleSelectionPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject garagePanel;
        [SerializeField] private GameObject racingPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Selectable mainInitialSelection;
        [SerializeField] private Selectable vehicleSelectionInitialSelection;
        [SerializeField] private Selectable settingsInitialSelection;
        [SerializeField] private Selectable garageInitialSelection;
        [SerializeField] private Selectable pauseInitialSelection;
        [SerializeField] private Selectable resultsInitialSelection;

        private GameObject _lastSelectedBeforeModal;

        public PresentationScreenState CurrentScreen { get; private set; } = PresentationScreenState.Main;

        public void Show(PresentationScreenState screenState)
        {
            var previous = CurrentScreen;
            CurrentScreen = screenState;
            var enteringModal = screenState == PresentationScreenState.Paused
                || screenState == PresentationScreenState.Results
                || screenState == PresentationScreenState.Settings;
            if (enteringModal && EventSystem.current != null)
            {
                _lastSelectedBeforeModal = EventSystem.current.currentSelectedGameObject;
            }

            SetActive(mainPanel, screenState == PresentationScreenState.Main);
            SetActive(vehicleSelectionPanel, screenState == PresentationScreenState.VehicleSelection);
            SetActive(settingsPanel, screenState == PresentationScreenState.Settings);
            SetActive(garagePanel, screenState == PresentationScreenState.Garage);
            SetActive(racingPanel, screenState == PresentationScreenState.Racing);
            SetActive(pausePanel, screenState == PresentationScreenState.Paused);
            SetActive(resultsPanel, screenState == PresentationScreenState.Results);

            if (previous == PresentationScreenState.Paused && screenState == PresentationScreenState.Racing)
            {
                RestorePreviousSelection();
                return;
            }

            SelectInitial(screenState);
        }

        public void RestorePreviousSelection()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(_lastSelectedBeforeModal);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void SelectInitial(PresentationScreenState screenState)
        {
            if (EventSystem.current == null)
            {
                return;
            }

            var selection = screenState switch
            {
                PresentationScreenState.Main => mainInitialSelection,
                PresentationScreenState.VehicleSelection => vehicleSelectionInitialSelection,
                PresentationScreenState.Settings => settingsInitialSelection,
                PresentationScreenState.Garage => garageInitialSelection,
                PresentationScreenState.Paused => pauseInitialSelection,
                PresentationScreenState.Results => resultsInitialSelection,
                _ => null
            };

            if (selection != null && selection.gameObject.activeInHierarchy && selection.interactable)
            {
                EventSystem.current.SetSelectedGameObject(selection.gameObject);
            }
        }
    }
}
