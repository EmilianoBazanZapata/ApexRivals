using ApexRivals.Audio.Runtime;
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
        [SerializeField] private GameObject trackSelectionPanel;
        [SerializeField] private Selectable mainInitialSelection;
        [SerializeField] private Selectable vehicleSelectionInitialSelection;
        [SerializeField] private Selectable settingsInitialSelection;
        [SerializeField] private Selectable garageInitialSelection;
        [SerializeField] private Selectable pauseInitialSelection;
        [SerializeField] private Selectable resultsInitialSelection;
        [SerializeField] private Selectable trackSelectionInitialSelection;

        private GameObject _lastSelectedBeforeModal;
        private readonly Dictionary<PresentationScreenState, GameObject> _lastSelections = new Dictionary<PresentationScreenState, GameObject>();
        private Coroutine _pendingSelectionRefresh;

        public PresentationScreenState CurrentScreen { get; private set; } = PresentationScreenState.Main;

        public bool IsGameplayActive => CurrentScreen == PresentationScreenState.Racing;

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
                UiFocus.Clear();
            }

            ApplyPanelVisibility(screenState);

            if (previous == PresentationScreenState.Paused && screenState == PresentationScreenState.Racing)
            {
                RestorePreviousSelection();
                return;
            }

            if (previous == PresentationScreenState.Settings && screenState == PresentationScreenState.Main)
            {
                SelectInitial(PresentationScreenState.Main);
                RefreshSelectionNextFrame(PresentationScreenState.Main);
                return;
            }

            SelectInitial(screenState);
            RefreshSelectionNextFrame(screenState);
        }

        public void NotifyNavigationInput()
        {
            RepairSelectionIfInvalid();
        }

        public void NotifyMouseInput()
        {
            // Focus is kept valid in every modality; mouse input only refreshes the remembered selection.
            RememberSelection(CurrentScreen);
        }

        public void NotifyCancelInput()
        {
            // An open modal owns Cancel; otherwise the current screen does. The module's own cancel
            // action is unbound, so this is the only dispatch path and must never bail out early.
            var root = UiFocus.HasActiveScope ? UiFocus.ActiveScopeRoot : ScreenPanel(CurrentScreen);
            if (root == null || EventSystem.current == null)
            {
                return;
            }

            if (!IsGameplayActive || UiFocus.HasActiveScope)
            {
                UiAudioEvents.Raise(UiSound.Back);
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            var eventData = new BaseEventData(EventSystem.current);
            if (selected != null
                && selected.transform.IsChildOf(root.transform)
                && ExecuteEvents.ExecuteHierarchy(selected, eventData, ExecuteEvents.cancelHandler) != null)
            {
                return;
            }

            ExecuteEvents.ExecuteHierarchy(root, eventData, ExecuteEvents.cancelHandler);
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
            SetActive(trackSelectionPanel, screenState == PresentationScreenState.TrackSelection);
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
                UiFocus.Request(_lastSelectedBeforeModal);
                return;
            }

            SelectInitial(CurrentScreen);
        }

        private void Update()
        {
            if (UiFocus.RepairActiveScope())
            {
                return;
            }

            if (EventSystem.current == null || CurrentSelectionIsUsable(ScreenPanel(CurrentScreen)))
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
                UiFocus.Request(selection);
                return;
            }

            if (!CurrentSelectionIsUsable(panel))
            {
                UiFocus.Clear();
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
                SelectInitial(screenState);
            }
        }

        private void RepairSelectionIfInvalid()
        {
            if (UiFocus.RepairActiveScope())
            {
                return;
            }

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
                PresentationScreenState.TrackSelection => trackSelectionInitialSelection,
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
                PresentationScreenState.TrackSelection => trackSelectionPanel,
                _ => null
            };
        }

        private static Selectable FirstUsableSelection(Selectable preferred, GameObject panel)
        {
            return IsUsable(preferred) ? preferred : UiFocus.FirstUsable(null, panel);
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
                    ? UiFocus.SelectableOf(selected)
                    : null;
        }

        private static bool IsUsable(Selectable selectable)
        {
            return UiFocus.IsUsable(selectable);
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

            return IsUsable(UiFocus.SelectableOf(selected));
        }
    }
}
