using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageUguiView : MonoBehaviour, IGarageView, ICancelHandler
    {
        [SerializeField] private Text currencyText;
        [SerializeField] private Text vehicleNameText;

        [SerializeField] private Text engineLevelText;
        [SerializeField] private Text enginePriceText;
        [SerializeField] private Text engineBenefitText;
        [SerializeField] private Image[] engineLevelSegments;
        [SerializeField] private Button engineUpgradeButton;
        [SerializeField] private Text engineUpgradeButtonLabel;

        [SerializeField] private Text handlingLevelText;
        [SerializeField] private Text handlingPriceText;
        [SerializeField] private Text handlingBenefitText;
        [SerializeField] private Image[] handlingLevelSegments;
        [SerializeField] private Button handlingUpgradeButton;
        [SerializeField] private Text handlingUpgradeButtonLabel;

        [SerializeField] private Text statsText;

        [SerializeField] private Button startRaceButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private Transform vehicleListRoot;
        [SerializeField] private GarageSectionTabController sectionTabs;
        [SerializeField] private GarageVehiclePreviewController vehiclePreview;

        [SerializeField] private Text statusText;
        [SerializeField] private GameObject feedbackRoot;


        private const string VehicleEquippedMessageKey = "ui.vehicleSelection.confirmed";

        private GaragePresenter _presenter;
        private bool _subscribed;
        private readonly List<Button> _vehicleButtons = new List<Button>();
        private Button _preferredVehicleButton;
        private bool _previewingVehicle;

        public void Bind(GaragePresenter presenter)
        {
            _presenter = presenter;
            if (presenter == null) Unsubscribe();
            else Subscribe();
        }

        public void Render(GarageViewModel viewModel)
        {
            if (this == null)
            {
                return;
            }

            UguiViewText.Set(currencyText, UguiViewText.FormatCurrency(viewModel.Currency));
            UguiViewText.Set(vehicleNameText, string.IsNullOrEmpty(viewModel.SelectedVehicleName) ? "NO VEHICLE SELECTED" : viewModel.SelectedVehicleName);
            RenderVehicleOptions(viewModel);
            ResolveSectionTabs();
            if (sectionTabs != null)
            {
                sectionTabs.SetVehicleSelectionState(viewModel.HasValidSelectedVehicle);
            }

            RenderUpgrade(
                viewModel.Engine,
                engineLevelText,
                enginePriceText,
                engineBenefitText,
                engineLevelSegments,
                engineUpgradeButton,
                engineUpgradeButtonLabel,
                UguiViewText.FormatEngineBenefit(viewModel.Engine.BenefitModifier),
                viewModel.OperationActive || !viewModel.HasValidSelectedVehicle);

            RenderUpgrade(
                viewModel.Handling,
                handlingLevelText,
                handlingPriceText,
                handlingBenefitText,
                handlingLevelSegments,
                handlingUpgradeButton,
                handlingUpgradeButtonLabel,
                UguiViewText.FormatHandlingBenefit(viewModel.Handling.BenefitModifier),
                viewModel.OperationActive || !viewModel.HasValidSelectedVehicle);

            // Stats follow the previewed vehicle so browsing the list shows what each car offers.
            var previewStats = viewModel.Vehicles != null
                && viewModel.CandidateVehicleIndex >= 0
                && viewModel.CandidateVehicleIndex < viewModel.Vehicles.Count
                    ? viewModel.Vehicles[viewModel.CandidateVehicleIndex].EffectiveStats
                    : viewModel.EffectiveStats;
            UguiViewText.Set(statsText, UguiViewText.FormatStats(previewStats));

            // Equipping reuses the Succeeded status; tell it apart from an upgrade purchase by its key.
            var feedback = viewModel.MessageKey == VehicleEquippedMessageKey
                ? "VEHICLE EQUIPPED"
                : UguiViewText.FormatPurchaseStatus(viewModel.PurchaseStatus);
            UguiViewText.Set(statusText, feedback);
            if (feedbackRoot != null)
            {
                feedbackRoot.SetActive(!string.IsNullOrEmpty(feedback));
            }

            SetButton(startRaceButton, !viewModel.OperationActive && viewModel.HasValidSelectedVehicle);
            SetButton(returnToMainMenuButton, !viewModel.OperationActive);
            ConfigureNavigation();
        }

        /// <summary>
        /// Explicit arrow/stick graph, rebuilt whenever interactability or the section changes:
        /// tabs on top, section content in the middle (vehicle list, or Engine ⇄ Handling cards),
        /// Back ⇄ Start Race at the bottom. Unusable targets are skipped (e.g. cards you cannot afford,
        /// so Back/Start Race go straight up to the tab).
        /// </summary>
        private void ConfigureNavigation()
        {
            ResolveSectionTabs();
            var vehiclesTab = sectionTabs != null ? sectionTabs.VehiclesTabButton : null;
            var upgradesTab = sectionTabs != null ? sectionTabs.UpgradesTabButton : null;
            var showingUpgrades = sectionTabs != null && sectionTabs.CurrentSection == GarageSection.Upgrades;
            var currentTab = showingUpgrades
                ? UiNavigation.First(upgradesTab, vehiclesTab)
                : UiNavigation.First(vehiclesTab, upgradesTab);
            var back = returnToMainMenuButton;
            var start = startRaceButton;

            Selectable belowTabs;
            Selectable aboveBack;
            Selectable aboveStart;

            if (showingUpgrades)
            {
                var engine = engineUpgradeButton;
                var handling = handlingUpgradeButton;
                UiNavigation.Set(engine, currentTab, UiNavigation.First(back, start), null, UiNavigation.First(handling));
                UiNavigation.Set(handling, currentTab, UiNavigation.First(start, back), UiNavigation.First(engine), null);
                belowTabs = UiNavigation.First(engine, handling, start, back);
                aboveBack = UiNavigation.First(engine, handling, currentTab);
                aboveStart = UiNavigation.First(handling, engine, currentTab);
            }
            else
            {
                var vehicles = new List<Selectable>(_vehicleButtons.Count);
                for (var index = 0; index < _vehicleButtons.Count; index++)
                {
                    if (UiNavigation.IsNavigable(_vehicleButtons[index]))
                    {
                        vehicles.Add(_vehicleButtons[index]);
                    }
                }

                var belowList = UiNavigation.First(start, back);
                for (var index = 0; index < vehicles.Count; index++)
                {
                    var up = index > 0 ? vehicles[index - 1] : currentTab;
                    var down = index < vehicles.Count - 1 ? vehicles[index + 1] : belowList;
                    UiNavigation.Set(vehicles[index], up, down, null, null);
                }

                var firstVehicle = vehicles.Count > 0 ? vehicles[0] : null;
                var lastVehicle = vehicles.Count > 0 ? vehicles[vehicles.Count - 1] : null;
                belowTabs = UiNavigation.First(_preferredVehicleButton, firstVehicle, start, back);
                aboveBack = UiNavigation.First(lastVehicle, currentTab);
                aboveStart = aboveBack;
            }

            UiNavigation.Set(vehiclesTab, null, belowTabs, null, UiNavigation.First(upgradesTab));
            UiNavigation.Set(upgradesTab, null, belowTabs, UiNavigation.First(vehiclesTab), null);
            UiNavigation.Set(back, aboveBack, null, null, UiNavigation.First(start));
            UiNavigation.Set(start, aboveStart, null, UiNavigation.First(back), null);
        }

        private void OnSectionChanged(GarageSection section)
        {
            ConfigureNavigation();
        }

        private void LateUpdate()
        {
            // Once focus leaves the vehicle list, fall back to the equipped car so the 3D preview,
            // stats and Start Race never disagree. Checked here because Deselect fires before the
            // new selection is known.
            if (!_previewingVehicle || IsVehicleButton(UiFocus.CurrentSelection))
            {
                return;
            }

            _previewingVehicle = false;
            _presenter?.CancelVehiclePreview();
        }

        private bool IsVehicleButton(GameObject candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            for (var index = 0; index < _vehicleButtons.Count; index++)
            {
                if (_vehicleButtons[index] != null && _vehicleButtons[index].gameObject == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void RenderVehicleOptions(GarageViewModel viewModel)
        {
            EnsureVehicleButtons(viewModel.Vehicles);

            if (vehiclePreview != null && viewModel.CandidateVehicleIndex >= 0 && viewModel.Vehicles != null
                && viewModel.CandidateVehicleIndex < viewModel.Vehicles.Count)
            {
                vehiclePreview.ShowVehicle(viewModel.Vehicles[viewModel.CandidateVehicleIndex].VehicleId);
            }
            for (var index = 0; index < _vehicleButtons.Count; index++)
            {
                var button = _vehicleButtons[index];
                if (button == null)
                {
                    continue;
                }

                var hasVehicle = viewModel.Vehicles != null && index < viewModel.Vehicles.Count;
                button.gameObject.SetActive(hasVehicle);
                if (!hasVehicle)
                {
                    continue;
                }

                var vehicle = viewModel.Vehicles[index];
                button.interactable = vehicle.CanSelect && !viewModel.OperationActive;
                var statusLabel = !vehicle.CanSelect ? "LOCKED" : vehicle.IsSelected ? "EQUIPPED" : "EQUIP";
                UguiViewText.Set(button.GetComponentInChildren<Text>(true), $"{vehicle.DisplayName.ToUpperInvariant()}\n{statusLabel}");
                var visualState = button.GetComponent<UguiMenuButtonVisualState>();
                if (visualState != null)
                {
                    visualState.IsCurrent = vehicle.IsSelected;
                }
            }

            SetVehicleTabInitialSelection(viewModel);
        }

        private void SetVehicleTabInitialSelection(GarageViewModel viewModel)
        {
            ResolveSectionTabs();
            if (sectionTabs == null || viewModel.Vehicles == null)
            {
                return;
            }

            Button fallback = null;
            for (var index = 0; index < _vehicleButtons.Count && index < viewModel.Vehicles.Count; index++)
            {
                var button = _vehicleButtons[index];
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                {
                    continue;
                }

                fallback ??= button;
                if (viewModel.Vehicles[index].IsSelected)
                {
                    _preferredVehicleButton = button;
                    sectionTabs.SetVehiclesInitialSelection(button);
                    return;
                }
            }

            if (fallback != null)
            {
                _preferredVehicleButton = fallback;
                sectionTabs.SetVehiclesInitialSelection(fallback);
            }
        }

        private void EnsureVehicleButtons(IReadOnlyList<VehicleSelectionItemViewModel> vehicles)
        {
            if (vehicles == null || vehicles.Count == 0)
            {
                return;
            }

            ResolveVehicleListRoot();
            if (vehicleListRoot == null)
            {
                return;
            }

            while (_vehicleButtons.Count < vehicles.Count)
            {
                var index = _vehicleButtons.Count;
                var button = CreateVehicleButton(vehicles[index].DisplayName);
                if (button == null)
                {
                    return;
                }

                var vehicleId = vehicles[index].VehicleId;
                // Focus (arrows, stick, hover) previews; click / Submit equips.
                button.onClick.AddListener(() => _presenter?.EquipVehicle(vehicleId));
                var trigger = button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                }

                var entry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                entry.callback.AddListener(_ =>
                {
                    _previewingVehicle = true;
                    _presenter?.SelectVehicle(vehicleId);
                });
                trigger.triggers.Add(entry);
                _vehicleButtons.Add(button);
            }
        }

        private Button CreateVehicleButton(string label)
        {
            var template = startRaceButton != null ? startRaceButton.gameObject : returnToMainMenuButton != null ? returnToMainMenuButton.gameObject : null;
            if (template == null)
            {
                return null;
            }

            var instance = Instantiate(template, vehicleListRoot);
            instance.name = $"{label}VehicleButton";
            var button = instance.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            var visualState = instance.GetComponent<UguiMenuButtonVisualState>();
            if (visualState != null)
            {
                // The template is StartRace, a primary action; list entries must not inherit its accent.
                visualState.PrimaryAction = false;
                visualState.IsCurrent = false;
            }

            UguiViewText.Set(instance.GetComponentInChildren<Text>(true), label);
            return button;
        }

        private void ResolveVehicleListRoot()
        {
            if (vehicleListRoot != null)
            {
                return;
            }

            var panel = FindChild(transform.root, "VehiclePanel");
            vehicleListRoot = panel != null ? panel : transform;
        }

        private void ResolveSectionTabs()
        {
            if (sectionTabs == null)
            {
                sectionTabs = transform.root.GetComponentInChildren<GarageSectionTabController>(true);
            }
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == childName)
            {
                return parent;
            }

            for (var index = 0; index < parent.childCount; index++)
            {
                var result = FindChild(parent.GetChild(index), childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void RenderUpgrade(
            GarageUpgradeViewModel upgrade,
            Text levelText,
            Text priceText,
            Text benefitText,
            Image[] segments,
            Button purchaseButton,
            Text purchaseButtonLabel,
            string benefitDisplay,
            bool operationActive)
        {
            UguiViewText.Set(levelText, $"LEVEL {upgrade.CurrentLevel} / {upgrade.MaximumLevel}");
            UguiViewText.Set(priceText, upgrade.IsAtMaximum ? "MAX LEVEL" : UguiViewText.FormatCurrency(upgrade.NextPrice));
            UguiViewText.Set(benefitText, benefitDisplay);
            UguiViewText.Set(purchaseButtonLabel, upgrade.IsAtMaximum ? "MAX LEVEL" : "UPGRADE");
            SetSegments(segments, upgrade.CurrentLevel, upgrade.MaximumLevel);
            SetButton(purchaseButton, upgrade.CanPurchase && !operationActive);
        }

        private void SetSegments(Image[] segments, int currentLevel, int maximumLevel)
        {
            if (segments == null)
            {
                return;
            }

            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                if (segment == null)
                {
                    continue;
                }

                var withinRange = index < maximumLevel;
                segment.gameObject.SetActive(withinRange);
                if (withinRange)
                {
                    segment.color = index < currentLevel ? ApexUiVisualStyle.PrimaryRed : ApexUiVisualStyle.Disabled;
                }
            }
        }

        private void OnEnable()
        {
            Subscribe();
            _presenter?.Present();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_previewingVehicle)
            {
                _previewingVehicle = false;
                _presenter?.CancelVehiclePreview();
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (engineUpgradeButton != null)
            {
                engineUpgradeButton.onClick.AddListener(OnEngineUpgradeClicked);
            }

            if (handlingUpgradeButton != null)
            {
                handlingUpgradeButton.onClick.AddListener(OnHandlingUpgradeClicked);
            }

            if (startRaceButton != null)
            {
                startRaceButton.onClick.AddListener(OnStartRaceClicked);
            }

            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);
            }

            ResolveSectionTabs();
            if (sectionTabs != null)
            {
                sectionTabs.SectionChanged += OnSectionChanged;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (engineUpgradeButton != null)
            {
                engineUpgradeButton.onClick.RemoveListener(OnEngineUpgradeClicked);
            }

            if (handlingUpgradeButton != null)
            {
                handlingUpgradeButton.onClick.RemoveListener(OnHandlingUpgradeClicked);
            }

            if (startRaceButton != null)
            {
                startRaceButton.onClick.RemoveListener(OnStartRaceClicked);
            }

            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);
            }

            if (sectionTabs != null)
            {
                sectionTabs.SectionChanged -= OnSectionChanged;
            }

            _subscribed = false;
        }

        private void OnEngineUpgradeClicked()
        {
            _presenter?.PurchaseEngine();
        }

        private void OnHandlingUpgradeClicked()
        {
            _presenter?.PurchaseHandling();
        }

        private async void OnStartRaceClicked()
        {
            if (_presenter != null)
            {
                await _presenter.StartRace();
            }
        }

        private async void OnReturnToMainMenuClicked()
        {
            if (_presenter != null)
            {
                await _presenter.ReturnToMainMenu();
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            OnReturnToMainMenuClicked();
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
