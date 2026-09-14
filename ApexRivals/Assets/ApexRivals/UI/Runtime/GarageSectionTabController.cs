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
        [SerializeField] private Image upgradesTabBackground;
        [SerializeField] private Image vehiclesTabBackground;
        [SerializeField] private GameObject upgradesPanel;
        [SerializeField] private GameObject vehiclePanel;
        [SerializeField] private Selectable upgradesInitialSelection;
        [SerializeField] private Selectable vehiclesInitialSelection;
        [SerializeField] private GameObject backgroundOverlay;
        [SerializeField] private Color selectedTabColor = new Color32(0xE5, 0x34, 0x2E, 0xFF);
        [SerializeField] private Color unselectedTabColor = new Color32(0x1B, 0x1E, 0x26, 0xFF);

        private bool _subscribed;
        private bool _hasValidSelectedVehicle;
        private bool _hasAppliedSection;

        public GarageSection CurrentSection { get; private set; } = GarageSection.Vehicles;

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
                    || !IsUsable(EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>())))
            {
                Focus(vehiclesInitialSelection);
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

            SetTabColor(upgradesTabBackground, section == GarageSection.Upgrades);
            SetTabColor(vehiclesTabBackground, section == GarageSection.Vehicles);

            if (section == GarageSection.Upgrades)
            {
                Focus(upgradesInitialSelection);
            }
            else if (section == GarageSection.Vehicles)
            {
                Focus(vehiclesInitialSelection);
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

        private static void Focus(Selectable selection)
        {
            if (EventSystem.current != null && IsUsable(selection))
            {
                EventSystem.current.SetSelectedGameObject(selection.gameObject);
            }
        }

        private static bool IsUsable(Selectable selectable)
        {
            return selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable();
        }

        private void SetTabColor(Image background, bool selected)
        {
            if (background != null)
            {
                background.color = selected ? selectedTabColor : unselectedTabColor;
            }
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
