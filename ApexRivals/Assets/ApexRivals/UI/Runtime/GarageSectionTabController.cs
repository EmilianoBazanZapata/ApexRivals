using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageSectionTabController : MonoBehaviour, IUiTabNavigation
    {
        private static readonly GarageSection[] TabOrder = { GarageSection.Vehicles, GarageSection.Upgrades };

        [SerializeField] private Button upgradesTabButton;
        [SerializeField] private Button vehiclesTabButton;
        [SerializeField] private GameObject upgradesPanel;
        [SerializeField] private GameObject vehiclePanel;
        [SerializeField] private Selectable upgradesInitialSelection;
        [SerializeField] private Selectable vehiclesInitialSelection;
        [SerializeField] private GameObject backgroundOverlay;

        private bool _subscribed;
        private bool _hasValidSelectedVehicle;
        private bool _hasAppliedSection;

        public GarageSection CurrentSection { get; private set; } = GarageSection.Vehicles;

        public Button VehiclesTabButton => vehiclesTabButton;

        public Button UpgradesTabButton => upgradesTabButton;

        /// <summary>Raised after a section is shown, so the view can rebuild its navigation graph.</summary>
        public event Action<GarageSection> SectionChanged;

        private void OnEnable()
        {
            Subscribe();
            ApplyTabAvailability();
            ShowSection(GarageSection.Vehicles);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (upgradesTabButton != null)
            {
                upgradesTabButton.onClick.AddListener(OnUpgradesTabClicked);
            }

            if (vehiclesTabButton != null)
            {
                vehiclesTabButton.onClick.AddListener(OnVehiclesTabClicked);
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (upgradesTabButton != null)
            {
                upgradesTabButton.onClick.RemoveListener(OnUpgradesTabClicked);
            }

            if (vehiclesTabButton != null)
            {
                vehiclesTabButton.onClick.RemoveListener(OnVehiclesTabClicked);
            }

            _subscribed = false;
        }

        private void OnUpgradesTabClicked()
        {
            ShowSection(GarageSection.Upgrades);
        }

        private void OnVehiclesTabClicked()
        {
            ShowSection(GarageSection.Vehicles);
        }

        public void SetVehicleSelectionState(bool hasValidSelectedVehicle)
        {
            _hasValidSelectedVehicle = hasValidSelectedVehicle;
            ApplyTabAvailability();
            if (!hasValidSelectedVehicle && CurrentSection == GarageSection.Upgrades)
            {
                ShowSection(GarageSection.Vehicles);
            }
        }

        public void SetVehiclesInitialSelection(Selectable selection)
        {
            if (selection == null || vehiclesInitialSelection == selection)
            {
                return;
            }

            vehiclesInitialSelection = selection;
            if (CurrentSection == GarageSection.Vehicles
                && EventSystem.current != null
                && (EventSystem.current.currentSelectedGameObject == null
                    || EventSystem.current.currentSelectedGameObject == vehiclesTabButton?.gameObject
                    || !IsRememberedSelectionEligible(UiFocus.SelectableOf(EventSystem.current.currentSelectedGameObject))))
            {
                Focus(vehiclesInitialSelection, vehiclesTabButton);
            }
        }

        public void ShowPreviousTab()
        {
            ShowAdjacentTab(-1);
        }

        public void ShowNextTab()
        {
            ShowAdjacentTab(1);
        }

        public void ShowSection(GarageSection section)
        {
            if (!_hasValidSelectedVehicle && section != GarageSection.Vehicles)
            {
                section = GarageSection.Vehicles;
            }

            if (_hasAppliedSection && CurrentSection == section)
            {
                return;
            }

            CurrentSection = section;
            _hasAppliedSection = true;

            SetActive(upgradesPanel, upgradesTabButton == null || section == GarageSection.Upgrades);
            SetActive(vehiclePanel, section == GarageSection.Vehicles);
            SetActive(backgroundOverlay, section != GarageSection.Vehicles);
            SetTabVisuals(section);

            SectionChanged?.Invoke(section);

            if (section == GarageSection.Upgrades)
            {
                Focus(upgradesInitialSelection, upgradesTabButton);
            }
            else if (section == GarageSection.Vehicles)
            {
                Focus(vehiclesInitialSelection, vehiclesTabButton);
            }
        }

        private void ApplyTabAvailability()
        {
            if (vehiclesTabButton != null)
            {
                vehiclesTabButton.interactable = true;
            }

            if (upgradesTabButton != null)
            {
                upgradesTabButton.interactable = _hasValidSelectedVehicle;
            }
        }

        private void SetTabVisuals(GarageSection section)
        {
            var vehiclesState = vehiclesTabButton != null ? vehiclesTabButton.GetComponent<UguiMenuButtonVisualState>() : null;
            var upgradesState = upgradesTabButton != null ? upgradesTabButton.GetComponent<UguiMenuButtonVisualState>() : null;
            if (vehiclesState != null)
            {
                vehiclesState.IsCurrent = section == GarageSection.Vehicles;
            }

            if (upgradesState != null)
            {
                upgradesState.IsCurrent = section == GarageSection.Upgrades;
            }
        }

        private void ShowAdjacentTab(int direction)
        {
            if (direction == 0 || TabOrder.Length == 0)
            {
                return;
            }

            var currentIndex = Array.IndexOf(TabOrder, CurrentSection);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = (currentIndex + direction + TabOrder.Length) % TabOrder.Length;
            ShowSection(TabOrder[nextIndex]);
        }

        private void Focus(Selectable selection, Selectable sectionTab)
        {
            // Content may be unusable (e.g. upgrades you cannot afford): fall back to the section tab.
            if (!UiFocus.Request(selection))
            {
                UiFocus.Request(UiNavigation.First(sectionTab, vehiclesTabButton, upgradesTabButton));
            }
        }

        private static bool IsRememberedSelectionEligible(Selectable selectable)
        {
            // Remembered selection keeps its existing hierarchy/interactability policy.
            // Focus() still applies the enabled-state check through UiFocus.Request.
            return selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable();
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
