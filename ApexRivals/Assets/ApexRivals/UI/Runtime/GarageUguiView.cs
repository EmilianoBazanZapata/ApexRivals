using System.Globalization;
using ApexRivals.Progression.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Single-screen garage ("SELECT VEHICLE"): a vehicle strip with previous/next on top, the car's name,
    /// position and ownership below it, stat bars bottom-left and a parts panel bottom-right (part tiles + the
    /// selected part's level, benefit and Upgrade button), then Back / Start Race.
    /// Previous/next (arrows, Q/E, LB/RB) equip the adjacent owned vehicle directly; a car that is not owned yet
    /// is only previewed, with its price, prerequisite and a Buy button.
    /// The legacy per-part card fields (engine/handling buttons, labels, segments) still work when assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageUguiView : MonoBehaviour, IGarageView, IUiVehicleSelectionNavigation, ICancelHandler
    {
        [SerializeField, Tooltip("Disable to preserve Navigation mode, links and wrapping authored on the Selectable components. Enable to rebuild links as controls appear/disappear.")] private bool useDynamicNavigation = true;

        [Header("Dynamic text templates")]
        [SerializeField] private string legacyLevelFormat = "LEVEL {0} / {1}";
        [SerializeField] private string engineBenefitFormat = "+{0:0}% TOP SPEED";
        [SerializeField] private string handlingBenefitFormat = "+{0:0}% GRIP";
        [SerializeField] private string purchasedMessage = "UPGRADE PURCHASED";
        [SerializeField] private string insufficientCurrencyMessage = "NOT ENOUGH CURRENCY";
        [SerializeField] private string maximumLevelMessage = "MAXIMUM LEVEL REACHED";

        [Header("Display text")]
        [SerializeField] private string noVehicleLabel = "NO VEHICLE SELECTED";
        [SerializeField] private string ownedLabel = "OWNED";
        [SerializeField] private string forSaleLabel = "FOR SALE";
        [SerializeField] private string lockedLabel = "LOCKED";
        [SerializeField] private string handlingLabel = "HANDLING";
        [SerializeField] private string engineLabel = "ENGINE";
        [SerializeField] private string handlingDescription = "IMPROVES STEERING RESPONSE AND GRIP.";
        [SerializeField] private string engineDescription = "INCREASES ACCELERATION AND TOP SPEED.";
        [SerializeField] private string maximumLevelLabel = "MAX LEVEL";
        [SerializeField] private string upgradeLabel = "UPGRADE";
        [SerializeField] private string partLevelFormat = "LEVEL  {0}/{1}";

        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private Text currencyText;
        [SerializeField] private Text vehicleNameText;

        [Header("Legacy upgrade cards (optional)")]
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

        [Header("Stats")]
        [SerializeField, Tooltip("Single-column fallback used when the label/value columns are not assigned.")] private Text statsText;
        [SerializeField] private Text statsLabelsText;
        [SerializeField] private Text statsValuesText;
        [SerializeField, Tooltip("Acceleration, Top Speed, Steering, Grip value texts (stat-bar layout).")] private Text[] statRowValues;
        [SerializeField, Tooltip("Matching bar fills; anchorMax.x is set to the stat ratio.")] private RectTransform[] statRowFills;

        [Header("Vehicle selector")]
        [SerializeField] private Button previousVehicleButton;
        [SerializeField] private Button nextVehicleButton;
        [SerializeField] private Text vehicleCounterText;
        [SerializeField, Tooltip("Short ownership tag next to the name: OWNED / FOR SALE / LOCKED.")] private Text vehicleBadgeText;
        [SerializeField, Tooltip("Strip slots, left to right; the middle slot is the vehicle on screen.")] private AngledPanelGraphic[] vehicleTileFrames;
        [SerializeField] private Text[] vehicleTileNames;
        [SerializeField, Tooltip("Automatically match previews to each sprite. Disable to preserve Aspect Ratio on each Image's AspectRatioFitter.")] private bool autoFitPreviewAspect = true;
        [SerializeField] private Image[] vehicleTileThumbnails;

        [Header("Parts panel (optional)")]
        [SerializeField] private Button engineTileButton;
        [SerializeField] private Button handlingTileButton;
        [SerializeField] private Text partNameText;
        [SerializeField, Tooltip("What the selected part improves.")] private Text partDescriptionText;
        [SerializeField] private Text partLevelText;
        [SerializeField] private Image[] partLevelSegments;
        [SerializeField] private Text partBenefitText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Text upgradeButtonLabel;
        [SerializeField] private Text upgradePriceText;
        [SerializeField, Tooltip("Price inset inside the Upgrade button; hidden at max level.")] private GameObject upgradePriceBox;

        [Header("Actions")]
        [SerializeField] private Button startRaceButton;
        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private GarageVehiclePreviewController vehiclePreview;

        [SerializeField] private Text statusText;
        [SerializeField] private GameObject feedbackRoot;

        [Header("Career (optional)")]
        [SerializeField, Tooltip("Owned, or price and prerequisite of the car on screen.")] private Text vehicleStatusText;
        [SerializeField] private Button buyVehicleButton;
        [SerializeField] private Text buyVehicleButtonLabel;
        [SerializeField] private CareerPanelUguiView careerPanel;

        private const string VehicleEquippedMessageKey = "ui.vehicleSelection.confirmed";
        [SerializeField, Min(1f)] private float statBarHeadroom = 1.15f;
        private readonly System.Collections.Generic.Dictionary<Image, Color> _thumbnailColors = new System.Collections.Generic.Dictionary<Image, Color>();

        private GaragePresenter _presenter;
        private bool _subscribed;
        private UpgradeType _selectedPart = UpgradeType.Engine;
        private GarageViewModel _lastModel;
        private bool _hasModel;

        private bool HasPartsPanel => upgradeButton != null;

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

            _lastModel = viewModel;
            _hasModel = true;

            UguiViewText.Set(currencyText, UguiViewText.FormatCurrency(viewModel.Currency));
            var shownName = viewModel.Career.HasCareer && !string.IsNullOrEmpty(viewModel.Career.CandidateVehicleName)
                ? viewModel.Career.CandidateVehicleName
                : viewModel.SelectedVehicleName;
            UguiViewText.Set(vehicleNameText, string.IsNullOrEmpty(shownName) ? noVehicleLabel : HasPartsPanel ? shownName.ToUpperInvariant() : shownName);
            RenderVehicleSelector(viewModel);
            RenderCareer(viewModel);

            var upgradesLocked = viewModel.OperationActive || !viewModel.HasValidSelectedVehicle;
            RenderUpgrade(
                viewModel.Engine,
                engineLevelText,
                enginePriceText,
                engineBenefitText,
                engineLevelSegments,
                engineUpgradeButton,
                engineUpgradeButtonLabel,
                UguiViewText.FormatEngineBenefit(viewModel.Engine.BenefitModifier, engineBenefitFormat),
                upgradesLocked);

            RenderUpgrade(
                viewModel.Handling,
                handlingLevelText,
                handlingPriceText,
                handlingBenefitText,
                handlingLevelSegments,
                handlingUpgradeButton,
                handlingUpgradeButtonLabel,
                UguiViewText.FormatHandlingBenefit(viewModel.Handling.BenefitModifier, handlingBenefitFormat),
                upgradesLocked);

            RenderPartsPanel(viewModel, upgradesLocked);
            RenderStats(viewModel);

            // The selector already shows the equipped vehicle, so only purchase results need a banner.
            var feedback = !string.IsNullOrEmpty(viewModel.Career.Feedback)
                ? viewModel.Career.Feedback
                : viewModel.MessageKey == VehicleEquippedMessageKey
                    ? string.Empty
                    : UguiViewText.FormatPurchaseStatus(viewModel.PurchaseStatus, purchasedMessage, insufficientCurrencyMessage, maximumLevelMessage);
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
        /// Explicit arrow/stick graph. Parts layout: vehicle arrows on top (Buy below them while a locked car is
        /// shown), Engine ⇄ Handling tiles, the Upgrade button under them, then Back ⇄ Start Race.
        /// Legacy layout: arrows, Engine ⇄ Handling buttons, Back ⇄ Start Race. Unusable targets are skipped.
        /// </summary>
        private void ConfigureNavigation()
        {
            if (!useDynamicNavigation) return;
            if (HasPartsPanel)
            {
                ConfigurePartsNavigation();
                return;
            }

            var previous = previousVehicleButton;
            var next = nextVehicleButton;
            var buy = buyVehicleButton;
            var engine = engineUpgradeButton;
            var handling = handlingUpgradeButton;
            var back = returnToMainMenuButton;
            var start = startRaceButton;

            // Buy (only while a locked car is on screen) sits between the car arrows and the upgrades.
            UiNavigation.Set(previous, null, UiNavigation.First(buy, engine, handling, back, start), null, UiNavigation.First(next));
            UiNavigation.Set(next, null, UiNavigation.First(buy, handling, engine, start, back), UiNavigation.First(previous), null);
            UiNavigation.Set(buy, UiNavigation.First(next, previous), UiNavigation.First(engine, handling, start, back), UiNavigation.First(previous), UiNavigation.First(next));
            UiNavigation.Set(engine, UiNavigation.First(buy, previous, next), UiNavigation.First(back, start), null, UiNavigation.First(handling));
            UiNavigation.Set(handling, UiNavigation.First(buy, next, previous), UiNavigation.First(start, back), UiNavigation.First(engine), null);
            UiNavigation.Set(back, UiNavigation.First(engine, handling, previous, next), null, null, UiNavigation.First(start));
            UiNavigation.Set(start, UiNavigation.First(handling, engine, next, previous), null, UiNavigation.First(back), null);
        }

        private void ConfigurePartsNavigation()
        {
            if (!useDynamicNavigation) return;
            var previous = previousVehicleButton;
            var next = nextVehicleButton;
            var buy = buyVehicleButton;
            var engine = engineTileButton;
            var handling = handlingTileButton;
            var upgrade = upgradeButton;
            var back = returnToMainMenuButton;
            var start = startRaceButton;
            var selectedTile = _selectedPart == UpgradeType.Handling ? UiNavigation.First(handling, engine) : UiNavigation.First(engine, handling);

            UiNavigation.Set(previous, null, UiNavigation.First(buy, engine, handling, upgrade, back, start), null, UiNavigation.First(next));
            UiNavigation.Set(next, null, UiNavigation.First(buy, handling, engine, upgrade, start, back), UiNavigation.First(previous), null);
            UiNavigation.Set(buy, UiNavigation.First(next, previous), UiNavigation.First(engine, handling, upgrade, start, back), UiNavigation.First(previous), UiNavigation.First(next));
            UiNavigation.Set(engine, UiNavigation.First(buy, previous, next), UiNavigation.First(upgrade, start, back), null, UiNavigation.First(handling));
            UiNavigation.Set(handling, UiNavigation.First(buy, next, previous), UiNavigation.First(upgrade, start, back), UiNavigation.First(engine), null);
            UiNavigation.Set(upgrade, UiNavigation.First(selectedTile, buy, next, previous), UiNavigation.First(start, back), null, null);
            UiNavigation.Set(back, UiNavigation.First(selectedTile, upgrade, buy, previous, next), null, null, UiNavigation.First(start));
            UiNavigation.Set(start, UiNavigation.First(upgrade, selectedTile, buy, next, previous), null, UiNavigation.First(back), null);
        }

        /// <summary>Gives the screen a usable focus on entry and when the focused control becomes unusable.</summary>
        private void EnsureFocus()
        {
            if (UiFocus.IsUsable(UiFocus.SelectableOf(UiFocus.CurrentSelection)))
            {
                return;
            }

            UiFocus.Request(UiNavigation.First(startRaceButton, engineTileButton, buyVehicleButton, nextVehicleButton, returnToMainMenuButton));
        }

        private void RenderCareer(GarageViewModel viewModel)
        {
            var career = viewModel.Career;
            if (careerPanel != null)
            {
                careerPanel.Render(career.Panel);
            }

            // With the badge layout the long status (price, prerequisite) only shows for cars you do not own.
            var detail = !career.HasCareer
                ? string.Empty
                : vehicleBadgeText != null && career.CandidateOwned ? string.Empty : career.CandidateStatus;
            UguiViewText.Set(vehicleStatusText, detail);
            if (vehicleStatusText != null)
            {
                vehicleStatusText.color = !career.HasCareer || career.CandidateOwned ? Palette.SecondaryText : Palette.AccentText;
            }

            if (vehicleBadgeText != null)
            {
                var badge = !career.HasCareer ? string.Empty : career.CandidateOwned ? ownedLabel : career.CanBuyCandidate ? forSaleLabel : lockedLabel;
                UguiViewText.Set(vehicleBadgeText, badge);
                vehicleBadgeText.color = career.CandidateOwned ? Palette.AccentText : Palette.SecondaryText;
            }

            if (buyVehicleButton != null)
            {
                var showBuy = career.HasCareer && !career.CandidateOwned;
                if (buyVehicleButton.gameObject.activeSelf != showBuy)
                {
                    buyVehicleButton.gameObject.SetActive(showBuy);
                }

                buyVehicleButton.interactable = career.CanBuyCandidate && !viewModel.OperationActive;
            }

            // Buy label is static, authored on its Text component.
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

            var shownIndex = viewModel.CandidateVehicleIndex >= 0 ? viewModel.CandidateVehicleIndex : equippedIndex;
            UguiViewText.Set(vehicleCounterText, HasPartsPanel && shownIndex >= 0 && count > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}/{1}", shownIndex + 1, count)
                : UguiViewText.FormatVehicleCounter(shownIndex, count));

            // Cycling needs another vehicle to move to.
            var canCycle = !viewModel.OperationActive && (selectableCount > 1 || (selectableCount == 1 && equippedIndex < 0));
            SetButton(previousVehicleButton, canCycle);
            SetButton(nextVehicleButton, canCycle);
            RenderVehicleStrip(viewModel, shownIndex, count);

            if (vehiclePreview != null && vehicles != null && viewModel.CandidateVehicleIndex >= 0 && viewModel.CandidateVehicleIndex < count)
            {
                vehiclePreview.ShowVehicle(vehicles[viewModel.CandidateVehicleIndex].VehicleId);
            }
        }

        /// <summary>
        /// The strip wraps like the previous/next cycle: the middle slot is the car on screen and the others are
        /// its neighbours. A slot shows the car's thumbnail when its definition has one, otherwise its name.
        /// </summary>
        private void RenderVehicleStrip(GarageViewModel viewModel, int shownIndex, int count)
        {
            if (vehicleTileFrames == null || vehicleTileFrames.Length == 0)
            {
                return;
            }

            var middle = vehicleTileFrames.Length / 2;
            for (var slot = 0; slot < vehicleTileFrames.Length; slot++)
            {
                var frame = vehicleTileFrames[slot];
                if (frame == null)
                {
                    continue;
                }

                var hasVehicle = count > 0 && shownIndex >= 0 && (count >= vehicleTileFrames.Length || Mathf.Abs(slot - middle) <= count / 2);
                if (frame.gameObject.activeSelf != hasVehicle)
                {
                    frame.gameObject.SetActive(hasVehicle);
                }

                if (!hasVehicle)
                {
                    continue;
                }

                var index = ((shownIndex + slot - middle) % count + count) % count;
                var vehicle = viewModel.Vehicles[index];
                // The car on screen is marked with a red outline and label, never the red fill: that fill is
                // reserved for the focused control so only one thing on screen ever reads as active.
                var current = slot == middle;
                frame.color = current ? Palette.PanelElevated : Palette.PanelDeep;
                frame.BorderColor = current ? Palette.PrimaryRed : Palette.PanelOutline;
                frame.BorderThickness = current ? Palette.VehicleFrameBorderSelected : Palette.VehicleFrameBorderResting;

                var thumbnail = vehicleTileThumbnails != null && slot < vehicleTileThumbnails.Length ? vehicleTileThumbnails[slot] : null;
                if (thumbnail != null)
                {
                    thumbnail.sprite = vehicle.Preview;
                    thumbnail.enabled = vehicle.Preview != null;
                    if (!_thumbnailColors.TryGetValue(thumbnail, out var authoredTint))
                    {
                        authoredTint = thumbnail.color;
                        _thumbnailColors.Add(thumbnail, authoredTint);
                    }
                    thumbnail.color = authoredTint * (vehicle.CanSelect ? Palette.AvailableThumbnailTint : Palette.UnavailableThumbnailTint);
                    UguiViewText.FitAspect(thumbnail, vehicle.Preview, autoFitPreviewAspect);
                }

                var label = vehicleTileNames != null && slot < vehicleTileNames.Length ? vehicleTileNames[slot] : null;
                if (label != null)
                {
                    UguiViewText.Set(label, vehicle.Preview != null ? string.Empty : ShortName(vehicle.DisplayName));
                    label.color = current
                        ? Palette.AccentText
                        : vehicle.CanSelect ? Palette.PrimaryText : Palette.DisabledText;
                }
            }
        }

        private static string ShortName(string displayName)
        {
            return string.IsNullOrEmpty(displayName) ? string.Empty : displayName.ToUpperInvariant();
        }

        /// <summary>Stat bars are relative to the best car in the garage (plus headroom for upgrades).</summary>
        private void RenderStats(GarageViewModel viewModel)
        {
            var stats = viewModel.Vehicles != null
                && viewModel.CandidateVehicleIndex >= 0
                && viewModel.CandidateVehicleIndex < viewModel.Vehicles.Count
                    ? viewModel.Vehicles[viewModel.CandidateVehicleIndex].EffectiveStats
                    : viewModel.EffectiveStats;

            if (statRowValues != null && statRowValues.Length > 0)
            {
                float[] values = { stats.Acceleration, stats.TopSpeed, stats.Steering, stats.Handling };
                for (var row = 0; row < values.Length && row < statRowValues.Length; row++)
                {
                    UguiViewText.Set(statRowValues[row], values[row].ToString(row == 3 ? "0.00" : "0.0", CultureInfo.InvariantCulture));
                    var fill = statRowFills != null && row < statRowFills.Length ? statRowFills[row] : null;
                    if (fill != null)
                    {
                        var max = MaxStat(viewModel, row, values[row]) * statBarHeadroom;
                        var anchor = fill.anchorMax;
                        anchor.x = max > 0f ? Mathf.Clamp01(values[row] / max) : 0f;
                        fill.anchorMax = anchor;
                    }
                }

                return;
            }

            if (statsValuesText != null)
            {
                // Stat labels are authored on statsLabelsText.
                UguiViewText.Set(statsValuesText, UguiViewText.FormatStatValues(stats));
            }
            else
            {
                UguiViewText.Set(statsText, UguiViewText.FormatStats(stats));
            }
        }

        private static float MaxStat(GarageViewModel viewModel, int row, float fallback)
        {
            var max = fallback;
            if (viewModel.Vehicles == null)
            {
                return max;
            }

            for (var index = 0; index < viewModel.Vehicles.Count; index++)
            {
                var stats = viewModel.Vehicles[index].EffectiveStats;
                var value = row == 0 ? stats.Acceleration : row == 1 ? stats.TopSpeed : row == 2 ? stats.Steering : stats.Handling;
                max = Mathf.Max(max, value);
            }

            return max;
        }

        private void RenderPartsPanel(GarageViewModel viewModel, bool upgradesLocked)
        {
            if (!HasPartsPanel)
            {
                return;
            }

            var part = _selectedPart == UpgradeType.Handling ? viewModel.Handling : viewModel.Engine;
            var benefit = _selectedPart == UpgradeType.Handling
                ? UguiViewText.FormatHandlingBenefit(part.BenefitModifier, handlingBenefitFormat)
                : UguiViewText.FormatEngineBenefit(part.BenefitModifier, engineBenefitFormat);

            UguiViewText.Set(partNameText, _selectedPart == UpgradeType.Handling ? handlingLabel : engineLabel);
            UguiViewText.Set(partDescriptionText, _selectedPart == UpgradeType.Handling
                ? handlingDescription
                : engineDescription);
            UguiViewText.Set(partLevelText, UguiViewText.FormatTemplate(partLevelFormat, "LEVEL  {0}/{1}", part.CurrentLevel, part.MaximumLevel));
            UguiViewText.Set(partBenefitText, string.IsNullOrEmpty(benefit) ? "-" : benefit.ToUpperInvariant());
            SetSegments(partLevelSegments, part.CurrentLevel, part.MaximumLevel);
            UguiViewText.Set(upgradeButtonLabel, part.IsAtMaximum ? maximumLevelLabel : upgradeLabel);
            UguiViewText.Set(upgradePriceText, part.IsAtMaximum ? string.Empty : UguiViewText.FormatCurrency(part.NextPrice));
            if (upgradePriceBox != null && upgradePriceBox.activeSelf == part.IsAtMaximum)
            {
                upgradePriceBox.SetActive(!part.IsAtMaximum);
            }
            SetButton(upgradeButton, part.CanPurchase && !upgradesLocked);

            SetTileCurrent(engineTileButton, _selectedPart == UpgradeType.Engine);
            SetTileCurrent(handlingTileButton, _selectedPart == UpgradeType.Handling);
            SetButton(engineTileButton, !viewModel.OperationActive);
            SetButton(handlingTileButton, !viewModel.OperationActive);
        }

        private static void SetTileCurrent(Button tile, bool current)
        {
            var visual = tile != null ? tile.GetComponent<UguiMenuButtonVisualState>() : null;
            if (visual != null)
            {
                visual.IsCurrent = current;
            }
        }

        private void SelectPart(UpgradeType part)
        {
            if (_selectedPart == part)
            {
                return;
            }

            _selectedPart = part;
            if (_hasModel)
            {
                RenderPartsPanel(_lastModel, _lastModel.OperationActive || !_lastModel.HasValidSelectedVehicle);
                ConfigureNavigation();
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
            UguiViewText.Set(levelText, UguiViewText.FormatTemplate(legacyLevelFormat, "LEVEL {0} / {1}", upgrade.CurrentLevel, upgrade.MaximumLevel));
            UguiViewText.Set(priceText, upgrade.IsAtMaximum ? maximumLevelLabel : UguiViewText.FormatCurrency(upgrade.NextPrice));
            UguiViewText.Set(benefitText, benefitDisplay);
            UguiViewText.Set(purchaseButtonLabel, upgrade.IsAtMaximum ? maximumLevelLabel : upgradeLabel);
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
                    segment.color = index < currentLevel ? Palette.PrimaryRed : Palette.Disabled;
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

            AddClick(engineUpgradeButton, OnEngineUpgradeClicked);
            AddClick(handlingUpgradeButton, OnHandlingUpgradeClicked);
            AddClick(startRaceButton, OnStartRaceClicked);
            AddClick(returnToMainMenuButton, OnReturnToMainMenuClicked);
            AddClick(previousVehicleButton, SelectPreviousVehicle);
            AddClick(nextVehicleButton, SelectNextVehicle);
            AddClick(buyVehicleButton, OnBuyVehicleClicked);
            AddClick(engineTileButton, OnEngineTileClicked);
            AddClick(handlingTileButton, OnHandlingTileClicked);
            AddClick(upgradeButton, OnUpgradeClicked);
            SetTileNotifier(engineTileButton, OnEngineTileClicked, true);
            SetTileNotifier(handlingTileButton, OnHandlingTileClicked, true);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            RemoveClick(engineUpgradeButton, OnEngineUpgradeClicked);
            RemoveClick(handlingUpgradeButton, OnHandlingUpgradeClicked);
            RemoveClick(startRaceButton, OnStartRaceClicked);
            RemoveClick(returnToMainMenuButton, OnReturnToMainMenuClicked);
            RemoveClick(previousVehicleButton, SelectPreviousVehicle);
            RemoveClick(nextVehicleButton, SelectNextVehicle);
            RemoveClick(buyVehicleButton, OnBuyVehicleClicked);
            RemoveClick(engineTileButton, OnEngineTileClicked);
            RemoveClick(handlingTileButton, OnHandlingTileClicked);
            RemoveClick(upgradeButton, OnUpgradeClicked);
            SetTileNotifier(engineTileButton, OnEngineTileClicked, false);
            SetTileNotifier(handlingTileButton, OnHandlingTileClicked, false);
            _subscribed = false;
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        /// <summary>Focusing a part tile (arrows, stick or hover) shows that part in the detail row.</summary>
        private static void SetTileNotifier(Button tile, System.Action action, bool subscribe)
        {
            var notifier = tile != null ? tile.GetComponent<UiSelectNotifier>() : null;
            if (notifier == null)
            {
                return;
            }

            if (subscribe) notifier.Selected += action;
            else notifier.Selected -= action;
        }

        private void OnEngineTileClicked()
        {
            SelectPart(UpgradeType.Engine);
        }

        private void OnHandlingTileClicked()
        {
            SelectPart(UpgradeType.Handling);
        }

        private void OnUpgradeClicked()
        {
            if (_selectedPart == UpgradeType.Handling)
            {
                _presenter?.PurchaseHandling();
            }
            else
            {
                _presenter?.PurchaseEngine();
            }
        }

        private void OnBuyVehicleClicked()
        {
            _presenter?.BuyCandidateVehicle();
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
