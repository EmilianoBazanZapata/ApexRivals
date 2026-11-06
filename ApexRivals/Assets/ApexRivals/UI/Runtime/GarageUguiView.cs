using System.Collections.Generic;
using System.Threading.Tasks;
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

        [SerializeField] private Color segmentFilledColor = new Color32(0xE5, 0x34, 0x2E, 0xFF);
        [SerializeField] private Color segmentEmptyColor = new Color32(0x2B, 0x2F, 0x38, 0xFF);

        private GaragePresenter _presenter;
        private bool _subscribed;
        private readonly List<Button> _vehicleButtons = new List<Button>();

        public void Bind(GaragePresenter presenter)
        {
            _presenter = presenter;
            Subscribe();
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

            UguiViewText.Set(statsText, UguiViewText.FormatStats(viewModel.EffectiveStats));

            var feedback = UguiViewText.FormatPurchaseStatus(viewModel.PurchaseStatus);
            UguiViewText.Set(statusText, feedback);
            if (feedbackRoot != null)
            {
                feedbackRoot.SetActive(!string.IsNullOrEmpty(feedback));
            }

            SetButton(startRaceButton, !viewModel.OperationActive && viewModel.HasValidSelectedVehicle);
            SetButton(returnToMainMenuButton, !viewModel.OperationActive);
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
                var statusLabel = !vehicle.CanSelect ? "LOCKED" : vehicle.IsSelected ? "SELECTED" : "SELECT";
                UguiViewText.Set(button.GetComponentInChildren<Text>(true), $"{vehicle.DisplayName}\n{statusLabel}");
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
                    sectionTabs.SetVehiclesInitialSelection(button);
                    return;
                }
            }

            if (fallback != null)
            {
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
                button.onClick.AddListener(() =>
                {
                    _presenter?.SelectVehicle(vehicleId);
                    _presenter?.ConfirmVehicle();
                });
                var trigger = button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                entry.callback.AddListener(_ => _presenter?.SelectVehicle(vehicleId));
                trigger.triggers.Add(entry);
                _vehicleButtons.Add(button);
            }

            for (var index = 0; index < _vehicleButtons.Count; index++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = index > 0 ? _vehicleButtons[index - 1] : null;
                navigation.selectOnDown = index < _vehicleButtons.Count - 1 ? _vehicleButtons[index + 1] : startRaceButton;
                _vehicleButtons[index].navigation = navigation;
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
                    segment.color = index < currentLevel ? segmentFilledColor : segmentEmptyColor;
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
                await Run(_presenter.StartRace());
            }
        }

        private async void OnReturnToMainMenuClicked()
        {
            if (_presenter != null)
            {
                await Run(_presenter.ReturnToMainMenu());
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            OnReturnToMainMenuClicked();
        }

        private static async Task Run(Task task)
        {
            await task;
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
