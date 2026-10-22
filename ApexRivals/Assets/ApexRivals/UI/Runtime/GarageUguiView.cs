using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Single-screen garage: equipped vehicle selector, stats, both upgrade cards and race/back actions.
    /// Previous/next (arrows, Q/E, LB/RB) equip the adjacent selectable vehicle directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageUguiView : MonoBehaviour, IGarageView, IUiVehicleSelectionNavigation, ICancelHandler
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

        [SerializeField, Tooltip("Single-column fallback used when the label/value columns are not assigned.")] private Text statsText;
        [SerializeField] private Text statsLabelsText;
        [SerializeField] private Text statsValuesText;

        [SerializeField] private Button previousVehicleButton;
        [SerializeField] private Button nextVehicleButton;
        [SerializeField] private Text vehicleCounterText;

        [SerializeField] private Button startRaceButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private GarageVehiclePreviewController vehiclePreview;

        [SerializeField] private Text statusText;
        [SerializeField] private GameObject feedbackRoot;


        private const string VehicleEquippedMessageKey = "ui.vehicleSelection.confirmed";

        private GaragePresenter _presenter;
        private bool _subscribed;

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
            RenderVehicleSelector(viewModel);

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

            var stats = viewModel.Vehicles != null
                && viewModel.CandidateVehicleIndex >= 0
                && viewModel.CandidateVehicleIndex < viewModel.Vehicles.Count
                    ? viewModel.Vehicles[viewModel.CandidateVehicleIndex].EffectiveStats
                    : viewModel.EffectiveStats;
            if (statsValuesText != null)
            {
                UguiViewText.Set(statsLabelsText, UguiViewText.StatLabels);
                UguiViewText.Set(statsValuesText, UguiViewText.FormatStatValues(stats));
            }
            else
            {
                UguiViewText.Set(statsText, UguiViewText.FormatStats(stats));
            }

            // The selector already shows the equipped vehicle, so only purchase results need a banner.
            var feedback = viewModel.MessageKey == VehicleEquippedMessageKey
                ? string.Empty
                : UguiViewText.FormatPurchaseStatus(viewModel.PurchaseStatus);
            UguiViewText.Set(statusText, feedback);
            if (feedbackRoot != null)
            {
                feedbackRoot.SetActive(!string.IsNullOrEmpty(feedback));
            }

            SetButton(startRaceButton, !viewModel.OperationActive && viewModel.HasValidSelectedVehicle);
            SetButton(returnToMainMenuButton, !viewModel.OperationActive);
            ConfigureNavigation();
            EnsureFocus();
        }

        public void SelectPreviousVehicle()
        {
            _presenter?.EquipPreviousVehicle();
        }

        public void SelectNextVehicle()
        {
            _presenter?.EquipNextVehicle();
        }

        /// <summary>
        /// Explicit arrow/stick graph: vehicle arrows on top, Engine ⇄ Handling in the middle and
        /// Back ⇄ Start Race at the bottom. Unusable targets are skipped (e.g. upgrades you cannot afford,
        /// so Back/Start Race go straight up to the vehicle arrows).
        /// </summary>
        private void ConfigureNavigation()
        {
            var previous = previousVehicleButton;
            var next = nextVehicleButton;
            var engine = engineUpgradeButton;
            var handling = handlingUpgradeButton;
            var back = returnToMainMenuButton;
            var start = startRaceButton;

            UiNavigation.Set(previous, null, UiNavigation.First(engine, handling, back, start), null, UiNavigation.First(next));
            UiNavigation.Set(next, null, UiNavigation.First(handling, engine, start, back), UiNavigation.First(previous), null);
            UiNavigation.Set(engine, UiNavigation.First(previous, next), UiNavigation.First(back, start), null, UiNavigation.First(handling));
            UiNavigation.Set(handling, UiNavigation.First(next, previous), UiNavigation.First(start, back), UiNavigation.First(engine), null);
            UiNavigation.Set(back, UiNavigation.First(engine, handling, previous, next), null, null, UiNavigation.First(start));
            UiNavigation.Set(start, UiNavigation.First(handling, engine, next, previous), null, UiNavigation.First(back), null);
        }

        /// <summary>Gives the screen a usable focus on entry and when the focused control becomes unusable.</summary>
        private void EnsureFocus()
        {
            if (UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)))
            {
                return;
            }

            UiFocus.Request(UiNavigation.First(startRaceButton, nextVehicleButton, returnToMainMenuButton));
        }

        private void RenderVehicleSelector(GarageViewModel viewModel)
        {
            var vehicles = viewModel.Vehicles;
            var count = vehicles != null ? vehicles.Count : 0;
            var equippedIndex = -1;
            var selectableCount = 0;
            for (var index = 0; index < count; index++)
            {
                if (vehicles[index].IsSelected)
                {
                    equippedIndex = index;
                }

                if (vehicles[index].CanSelect)
                {
                    selectableCount++;
                }
            }

            var shownIndex = equippedIndex >= 0 ? equippedIndex : viewModel.CandidateVehicleIndex;
            UguiViewText.Set(vehicleCounterText, UguiViewText.FormatVehicleCounter(shownIndex, count));

            // Cycling needs another vehicle to move to.
            var canCycle = !viewModel.OperationActive && (selectableCount > 1 || (selectableCount == 1 && equippedIndex < 0));
            SetButton(previousVehicleButton, canCycle);
            SetButton(nextVehicleButton, canCycle);

            if (vehiclePreview != null && vehicles != null && viewModel.CandidateVehicleIndex >= 0 && viewModel.CandidateVehicleIndex < count)
            {
                vehiclePreview.ShowVehicle(vehicles[viewModel.CandidateVehicleIndex].VehicleId);
            }
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

            if (previousVehicleButton != null)
            {
                previousVehicleButton.onClick.AddListener(SelectPreviousVehicle);
            }

            if (nextVehicleButton != null)
            {
                nextVehicleButton.onClick.AddListener(SelectNextVehicle);
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

            if (previousVehicleButton != null)
            {
                previousVehicleButton.onClick.RemoveListener(SelectPreviousVehicle);
            }

            if (nextVehicleButton != null)
            {
                nextVehicleButton.onClick.RemoveListener(SelectNextVehicle);
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
