using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class UguiScreenNavigator : MonoBehaviour, IPresentationScreenNavigator, IUiFocusCoordinator
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
        private readonly Dictionary<PresentationScreenState, GameObject> _lastSelections = new Dictionary<PresentationScreenState, GameObject>();
        private Coroutine _pendingSelectionRefresh;
        private bool _navigationInputActive = true;

        public PresentationScreenState CurrentScreen { get; private set; } = PresentationScreenState.Main;

        private void OnEnable()
        {
            ApplyPanelVisibility(CurrentScreen);
            SelectInitial(CurrentScreen);
        }

        public void Show(PresentationScreenState screenState)
        {
            var previous = CurrentScreen;
            RememberSelection(previous);
            CurrentScreen = screenState;
            var enteringModal = screenState == PresentationScreenState.Paused
                || screenState == PresentationScreenState.Results
                || screenState == PresentationScreenState.Settings;
            if (enteringModal && EventSystem.current != null)
            {
                _lastSelectedBeforeModal = EventSystem.current.currentSelectedGameObject;
            }

            if (previous != screenState && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            ApplyPanelVisibility(screenState);
            LogSelection("After panel visibility", screenState);

            if (previous == PresentationScreenState.Paused && screenState == PresentationScreenState.Racing)
            {
                RestorePreviousSelection();
                return;
            }

            if (previous == PresentationScreenState.Settings && screenState == PresentationScreenState.Main)
            {
                _navigationInputActive = true;
                SelectInitial(PresentationScreenState.Main);
                LogSelection("Settings -> Main immediate selection", PresentationScreenState.Main);
                RefreshSelectionNextFrame(PresentationScreenState.Main);
                return;
            }

            SelectInitial(screenState);
            LogSelection("After SelectInitial", screenState);
            RefreshSelectionNextFrame(screenState);
        }

        public void NotifyNavigationInput()
        {
            _navigationInputActive = true;
            LogSelection("Navigation input before repair", CurrentScreen);
            RepairSelectionIfInvalid();
            LogSelection("Navigation input after repair", CurrentScreen);
        }

        public void NotifyMouseInput()
        {
            _navigationInputActive = false;
            RememberSelection(CurrentScreen);
        }

        public void NotifyCancelInput()
        {
            var panel = ScreenPanel(CurrentScreen);
            LogSelection("Cancel input", CurrentScreen);
            if (panel == null || EventSystem.current == null)
            {
                return;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.GetComponent<ICancelHandler>() != null)
            {
                return;
            }

            var eventData = new BaseEventData(EventSystem.current);
            if (selected != null && selected.transform.IsChildOf(panel.transform))
            {
                ExecuteEvents.ExecuteHierarchy(selected, eventData, ExecuteEvents.cancelHandler);
                return;
            }

            ExecuteEvents.ExecuteHierarchy(panel, eventData, ExecuteEvents.cancelHandler);
        }

        private void ApplyPanelVisibility(PresentationScreenState screenState)
        {
            SetActive(mainPanel, screenState == PresentationScreenState.Main);
            SetActive(vehicleSelectionPanel, screenState == PresentationScreenState.VehicleSelection);
            SetActive(settingsPanel, screenState == PresentationScreenState.Settings);
            SetActive(garagePanel, screenState == PresentationScreenState.Garage);
            SetActive(racingPanel, screenState == PresentationScreenState.Racing);
            SetActive(pausePanel, screenState == PresentationScreenState.Paused);
            SetActive(resultsPanel, screenState == PresentationScreenState.Results);
        }

        public void RestorePreviousSelection()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            var currentPanel = ScreenPanel(CurrentScreen);
            if (IsUsableSelection(_lastSelectedBeforeModal, currentPanel))
            {
                EventSystem.current.SetSelectedGameObject(_lastSelectedBeforeModal);
                return;
            }

            SelectInitial(CurrentScreen);
        }

        private void Update()
        {
            if (!_navigationInputActive || EventSystem.current == null || CurrentSelectionIsUsable(ScreenPanel(CurrentScreen)))
            {
                return;
            }

            RepairSelectionIfInvalid();
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

            var panel = ScreenPanel(screenState);
            var selection = PreviousSelection(screenState);
            if (selection == null || !IsUsable(selection))
            {
                selection = InitialSelection(screenState);
            }

            selection = FirstUsableSelection(selection, panel);
            if (selection != null)
            {
                EventSystem.current.SetSelectedGameObject(selection.gameObject);
                LogSelection($"SelectInitial picked {NameOf(selection.gameObject)}", screenState);
                return;
            }

            if (!CurrentSelectionIsUsable(panel))
            {
                EventSystem.current.SetSelectedGameObject(null);
                LogSelection("SelectInitial cleared selection", screenState);
            }
        }

        private void RefreshSelectionNextFrame(PresentationScreenState screenState)
        {
            if (_pendingSelectionRefresh != null)
            {
                StopCoroutine(_pendingSelectionRefresh);
            }

            _pendingSelectionRefresh = StartCoroutine(RefreshSelectionAfterPanelSwitch(screenState));
        }

        private System.Collections.IEnumerator RefreshSelectionAfterPanelSwitch(PresentationScreenState screenState)
        {
            yield return null;

            _pendingSelectionRefresh = null;
            if (CurrentScreen != screenState || EventSystem.current == null)
            {
                yield break;
            }

            if (!CurrentSelectionIsUsable(ScreenPanel(screenState)))
            {
                LogSelection("Deferred selection invalid before repair", screenState);
                SelectInitial(screenState);
                LogSelection("Deferred selection after repair", screenState);
            }
            else
            {
                LogSelection("Deferred selection already valid", screenState);
            }
        }

        private void RepairSelectionIfInvalid()
        {
            if (EventSystem.current == null || CurrentSelectionIsUsable(ScreenPanel(CurrentScreen)))
            {
                return;
            }

            SelectInitial(CurrentScreen);
        }

        private Selectable InitialSelection(PresentationScreenState screenState)
        {
            return screenState switch
            {
                PresentationScreenState.Main => mainInitialSelection,
                PresentationScreenState.VehicleSelection => vehicleSelectionInitialSelection,
                PresentationScreenState.Settings => settingsInitialSelection,
                PresentationScreenState.Garage => garageInitialSelection,
                PresentationScreenState.Paused => pauseInitialSelection,
                PresentationScreenState.Results => resultsInitialSelection,
                _ => null
            };
        }

        private GameObject ScreenPanel(PresentationScreenState screenState)
        {
            return screenState switch
            {
                PresentationScreenState.Main => mainPanel,
                PresentationScreenState.VehicleSelection => vehicleSelectionPanel,
                PresentationScreenState.Settings => settingsPanel,
                PresentationScreenState.Garage => garagePanel,
                PresentationScreenState.Racing => racingPanel,
                PresentationScreenState.Paused => pausePanel,
                PresentationScreenState.Results => resultsPanel,
                _ => null
            };
        }

        private static Selectable FirstUsableSelection(Selectable preferred, GameObject panel)
        {
            if (IsUsable(preferred))
            {
                return preferred;
            }

            if (panel == null || !panel.activeInHierarchy)
            {
                return null;
            }

            var selectables = panel.GetComponentsInChildren<Selectable>(true);
            for (var index = 0; index < selectables.Length; index++)
            {
                if (IsUsable(selectables[index]))
                {
                    return selectables[index];
                }
            }

            return null;
        }

        private void RememberSelection(PresentationScreenState screenState)
        {
            if (EventSystem.current == null)
            {
                return;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            if (IsUsableSelection(selected, ScreenPanel(screenState)))
            {
                _lastSelections[screenState] = selected;
            }
        }

        private Selectable PreviousSelection(PresentationScreenState screenState)
        {
            return _lastSelections.TryGetValue(screenState, out var selected)
                && IsUsableSelection(selected, ScreenPanel(screenState))
                    ? selected.GetComponent<Selectable>()
                    : null;
        }

        private static bool IsUsable(Selectable selectable)
        {
            return selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable();
        }

        private static bool CurrentSelectionIsUsable(GameObject currentPanel)
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return false;
            }

            return IsUsableSelection(EventSystem.current.currentSelectedGameObject, currentPanel);
        }

        private static bool IsUsableSelection(GameObject selected, GameObject currentPanel)
        {
            if (selected == null || currentPanel == null || !selected.transform.IsChildOf(currentPanel.transform))
            {
                return false;
            }

            return IsUsable(selected.GetComponent<Selectable>());
        }

        private void LogSelection(string message, PresentationScreenState screenState)
        {
            var panel = ScreenPanel(screenState);
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        }

        private static string NameOf(Object target)
        {
            return target != null ? target.name : "null";
        }
    }
}
