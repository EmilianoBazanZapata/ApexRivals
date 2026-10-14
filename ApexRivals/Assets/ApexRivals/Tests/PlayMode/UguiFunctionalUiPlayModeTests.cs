using System.Collections;
using System.IO;
using System.Reflection;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class UguiFunctionalUiPlayModeTests
    {
        private readonly PlayModeSceneIsolation _isolation = new PlayModeSceneIsolation();

        [SetUp]
        public void SetUp()
        {
            _isolation.Capture();
        }

        [TearDown]
        public void TearDown()
        {
            _isolation.Cleanup();
        }

        [UnityTest]
        public IEnumerator OpeningSettingsSelectsInitialControl()
        {
            var eventSystem = CreateEventSystem();
            var navigatorObject = new GameObject("Navigator");
            var navigator = navigatorObject.AddComponent<UguiScreenNavigator>();
            var main = new GameObject("Main");
            var settings = new GameObject("Settings");
            var mainButton = main.AddComponent<Button>();
            var settingsButton = settings.AddComponent<Button>();
            Set(navigator, "mainPanel", main);
            Set(navigator, "settingsPanel", settings);
            Set(navigator, "mainInitialSelection", mainButton);
            Set(navigator, "settingsInitialSelection", settingsButton);

            yield return null;
            navigator.Show(PresentationScreenState.Settings);
            yield return null;
            yield return null;

            Assert.That(main.activeSelf, Is.False);
            Assert.That(settings.activeSelf, Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(settingsButton.gameObject));
        }

        [UnityTest]
        public IEnumerator ClosingPauseRestoresPreviousSelection()
        {
            var eventSystem = CreateEventSystem();
            var navigatorObject = new GameObject("Navigator");
            var navigator = navigatorObject.AddComponent<UguiScreenNavigator>();
            var racing = new GameObject("Racing");
            var pause = new GameObject("Pause");
            var racingButton = racing.AddComponent<Button>();
            var pauseButton = pause.AddComponent<Button>();
            Set(navigator, "racingPanel", racing);
            Set(navigator, "pausePanel", pause);
            Set(navigator, "pauseInitialSelection", pauseButton);
            yield return null;
            eventSystem.SetSelectedGameObject(racingButton.gameObject);

            navigator.Show(PresentationScreenState.Paused);
            navigator.Show(PresentationScreenState.Racing);
            yield return null;

            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(racingButton.gameObject));
        }

        [UnityTest]
        public IEnumerator GaragePurchaseButtonsReflectPresenterState()
        {
            var view = new GameObject("GarageView").AddComponent<GarageUguiView>();
            var engineButton = new GameObject("Engine").AddComponent<Button>();
            var handlingButton = new GameObject("Handling").AddComponent<Button>();
            Set(view, "engineUpgradeButton", engineButton);
            Set(view, "handlingUpgradeButton", handlingButton);

            var model = new GarageViewModel(
                0,
                "Starter",
                new GarageUpgradeViewModel(UpgradeType.Engine, 3, 3, 0, false, UpgradeStatModifier.Identity),
                new GarageUpgradeViewModel(UpgradeType.Handling, 0, 3, 200, false, UpgradeStatModifier.Identity),
                CreateStats(),
                false,
                PresentationStatus.MaximumLevelReached,
                "ui.garage.maximumLevel");

            view.Render(model);
            yield return null;

            Assert.That(engineButton.interactable, Is.False);
            Assert.That(handlingButton.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator ResultsSuppressesPausePanelVisibility()
        {
            var navigatorObject = new GameObject("Navigator");
            var navigator = navigatorObject.AddComponent<UguiScreenNavigator>();
            var pause = new GameObject("Pause");
            var results = new GameObject("Results");
            Set(navigator, "pausePanel", pause);
            Set(navigator, "resultsPanel", results);

            navigator.Show(PresentationScreenState.Paused);
            navigator.Show(PresentationScreenState.Results);
            yield return null;

            Assert.That(pause.activeSelf, Is.False);
            Assert.That(results.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator MainMenuNavigationWrapsAndSkipsDisabledSettings()
        {
            var play = CreateButton("PlayButton");
            var garage = CreateButton("GarageButton");
            var settings = CreateButton("SettingsButton");
            var exit = CreateButton("ExitButton");
            settings.interactable = false;
            var controller = new GameObject("MainMenuNavigation").AddComponent<UguiMainMenuNavigationController>();
            Set(controller, "playButton", play);
            Set(controller, "garageButton", garage);
            Set(controller, "settingsButton", settings);
            Set(controller, "exitButton", exit);

            yield return null;
            controller.RefreshNavigation();
            yield return null;

            Assert.That(play.navigation.selectOnDown, Is.EqualTo(garage));
            Assert.That(garage.navigation.selectOnDown, Is.EqualTo(exit));
            Assert.That(exit.navigation.selectOnDown, Is.EqualTo(play));
            Assert.That(play.navigation.selectOnUp, Is.EqualTo(exit));
        }

        [UnityTest]
        public IEnumerator MainMenuNavigationRestoresFocusWhenSelectionIsCleared()
        {
            // UguiMainMenuNavigationController only wires navigation; UguiScreenNavigator owns focus.
            var eventSystem = CreateEventSystem();
            var mainPanel = new GameObject("MainPanel");
            var play = CreateButton("PlayButton");
            var garage = CreateButton("GarageButton");
            play.transform.SetParent(mainPanel.transform, false);
            garage.transform.SetParent(mainPanel.transform, false);
            var controller = mainPanel.AddComponent<UguiMainMenuNavigationController>();
            Set(controller, "playButton", play);
            Set(controller, "garageButton", garage);
            var navigatorObject = new GameObject("Navigator");
            navigatorObject.SetActive(false);
            var navigator = navigatorObject.AddComponent<UguiScreenNavigator>();
            Set(navigator, "mainPanel", mainPanel);
            Set(navigator, "mainInitialSelection", play);
            navigatorObject.SetActive(true);

            yield return null;
            controller.RefreshNavigation();
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(play.gameObject));
            Assert.That(play.navigation.selectOnDown, Is.EqualTo(garage));

            eventSystem.SetSelectedGameObject(null);
            yield return null;

            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(play.gameObject));
        }

        [UnityTest]
        public IEnumerator MenuButtonVisualStateReflectsSelectedAndDisabledStates()
        {
            var eventSystem = CreateEventSystem();
            var button = CreateButton("PlayButton");
            var image = button.gameObject.AddComponent<Image>();
            var label = new GameObject("Label").AddComponent<Text>();
            label.transform.SetParent(button.transform, false);
            var visual = button.gameObject.AddComponent<UguiMenuButtonVisualState>();
            Set(visual, "button", button);
            Set(visual, "background", image);
            Set(visual, "labels", new[] { label });

            yield return null;
            button.Select();
            yield return null;
            yield return null;
            Assert.That(image.color.r, Is.GreaterThan(image.color.g));
            Assert.That(label.color, Is.EqualTo(Color.white));

            button.interactable = false;
            yield return null;

            Assert.That(image.color, Is.EqualTo(ApexUiVisualStyle.Disabled));
            Assert.That(label.color, Is.EqualTo(ApexUiVisualStyle.DisabledText));
        }

        [UnityTest]
        public IEnumerator MenuButtonVisualStateKeepsOnlyEventSystemSelectionHighlighted()
        {
            var eventSystem = CreateEventSystem();
            var first = CreateButton("FirstButton");
            var second = CreateButton("SecondButton");
            var firstImage = first.gameObject.AddComponent<Image>();
            var secondImage = second.gameObject.AddComponent<Image>();
            var firstVisual = first.gameObject.AddComponent<UguiMenuButtonVisualState>();
            var secondVisual = second.gameObject.AddComponent<UguiMenuButtonVisualState>();
            Set(firstVisual, "button", first);
            Set(firstVisual, "background", firstImage);
            Set(secondVisual, "button", second);
            Set(secondVisual, "background", secondImage);

            yield return null;
            first.Select();
            yield return null;
            var firstSelectedColor = firstImage.color;
            var secondNormalColor = secondImage.color;

            var pointerEvent = new PointerEventData(eventSystem);
            firstVisual.OnPointerEnter(pointerEvent);
            eventSystem.SetSelectedGameObject(second.gameObject);
            yield return null;

            Assert.That(firstImage.color, Is.EqualTo(secondNormalColor));
            Assert.That(secondImage.color, Is.EqualTo(firstSelectedColor));
        }

        [UnityTest]
        public IEnumerator SettingsAndPauseRepairClearedSelection()
        {
            var eventSystem = CreateEventSystem();
            var navigator = new GameObject("Navigator").AddComponent<UguiScreenNavigator>();
            var settings = new GameObject("Settings");
            var settingsButton = CreateButton("DisplayTab");
            settingsButton.transform.SetParent(settings.transform, false);
            var pause = new GameObject("Pause");
            var pauseButton = CreateButton("Continue");
            pauseButton.transform.SetParent(pause.transform, false);
            Set(navigator, "settingsPanel", settings);
            Set(navigator, "settingsInitialSelection", settingsButton);
            Set(navigator, "pausePanel", pause);
            Set(navigator, "pauseInitialSelection", pauseButton);

            yield return null;
            navigator.Show(PresentationScreenState.Settings);
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(settingsButton.gameObject));
            eventSystem.SetSelectedGameObject(null);
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(settingsButton.gameObject));

            navigator.Show(PresentationScreenState.Paused);
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(pauseButton.gameObject));
            eventSystem.SetSelectedGameObject(null);
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(pauseButton.gameObject));
        }

        [UnityTest]
        public IEnumerator DisabledPointerInputLeavesUiPointerActionsNull()
        {
            var actions = InputActionAsset.FromJson(@"
            {
              ""name"": ""UiPointerTest"",
              ""maps"": [{
                ""name"": ""UI"",
                ""id"": ""11111111111111111111111111111111"",
                ""actions"": [
                  { ""name"": ""Navigate"", ""type"": ""Value"", ""id"": ""11111111111111111111111111111112"", ""expectedControlType"": ""Vector2"" },
                  { ""name"": ""Submit"", ""type"": ""Button"", ""id"": ""11111111111111111111111111111113"" },
                  { ""name"": ""Cancel"", ""type"": ""Button"", ""id"": ""11111111111111111111111111111114"" },
                  { ""name"": ""Point"", ""type"": ""PassThrough"", ""id"": ""11111111111111111111111111111115"", ""expectedControlType"": ""Vector2"" },
                  { ""name"": ""Click"", ""type"": ""Button"", ""id"": ""11111111111111111111111111111116"" },
                  { ""name"": ""RightClick"", ""type"": ""Button"", ""id"": ""11111111111111111111111111111117"" },
                  { ""name"": ""MiddleClick"", ""type"": ""Button"", ""id"": ""11111111111111111111111111111118"" },
                  { ""name"": ""ScrollWheel"", ""type"": ""PassThrough"", ""id"": ""11111111111111111111111111111119"", ""expectedControlType"": ""Vector2"" },
                  { ""name"": ""PreviousTab"", ""type"": ""Button"", ""id"": ""1111111111111111111111111111111a"" },
                  { ""name"": ""NextTab"", ""type"": ""Button"", ""id"": ""1111111111111111111111111111111b"" }
                ],
                ""bindings"": []
              }]
            }");
            var owner = new GameObject("UI Input Owner");
            owner.AddComponent<EventSystem>();
            var inputModule = owner.AddComponent<InputSystemUIInputModule>();
            var input = owner.AddComponent<BootstrapUiInputSystem>();
            Set(input, "inputActions", actions);
            Set(input, "inputModule", inputModule);
            Set(input, "allowPointerInput", false);
            typeof(BootstrapUiInputSystem)
                .GetMethod("ConfigureInputModule", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(input, null);

            yield return null;

            Assert.That(inputModule.point, Is.Null);
            Assert.That(inputModule.leftClick, Is.Null);
            Assert.That(inputModule.rightClick, Is.Null);
            Assert.That(inputModule.middleClick, Is.Null);
            Assert.That(inputModule.scrollWheel, Is.Null);
            Object.Destroy(owner);
            Object.Destroy(actions);
        }

        [UnityTest]
        public IEnumerator PauseDoesNotChangeCentralCursorVisibility()
        {
            var previousVisibility = Cursor.visible;
            var previousLock = Cursor.lockState;
            Cursor.visible = false;
            var pauseObject = new GameObject("Pause View");
            var pauseView = pauseObject.AddComponent<PauseUguiView>();

            pauseView.Render(new PauseViewModel(true, false, true, true, true, PresentationFailure.None, string.Empty));
            yield return null;
            Assert.That(Cursor.visible, Is.False);
            pauseView.Render(new PauseViewModel(false, false, false, false, false, PresentationFailure.None, string.Empty));
            yield return null;
            Assert.That(Cursor.visible, Is.False);

            Object.Destroy(pauseObject);
            Cursor.visible = previousVisibility;
            Cursor.lockState = previousLock;
        }

        [UnityTest]
        public IEnumerator GarageTabs_WrapSwitchPanelsAndRestoreValidFocus()
        {
            var eventSystem = CreateEventSystem();
            var vehiclePanel = new GameObject("VehiclePanel");
            var upgradesPanel = new GameObject("UpgradesPanel");
            var vehicleOption = CreateButton("SelectedVehicle");
            vehicleOption.transform.SetParent(vehiclePanel.transform, false);
            var engineUpgrade = CreateButton("EngineUpgrade");
            engineUpgrade.transform.SetParent(upgradesPanel.transform, false);
            var vehiclesTab = CreateButton("VehiclesTab");
            var upgradesTab = CreateButton("UpgradesTab");
            var controllerObject = new GameObject("GarageTabs");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<GarageSectionTabController>();
            Set(controller, "vehiclesTabButton", vehiclesTab);
            Set(controller, "upgradesTabButton", upgradesTab);
            Set(controller, "vehiclePanel", vehiclePanel);
            Set(controller, "upgradesPanel", upgradesPanel);
            Set(controller, "vehiclesInitialSelection", vehicleOption);
            Set(controller, "upgradesInitialSelection", engineUpgrade);
            controllerObject.SetActive(true);
            controller.SetVehicleSelectionState(true);

            yield return null;
            Assert.That(controller.CurrentSection, Is.EqualTo(GarageSection.Vehicles));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(vehicleOption.gameObject));

            controller.ShowNextTab();
            yield return null;
            Assert.That(controller.CurrentSection, Is.EqualTo(GarageSection.Upgrades));
            Assert.That(vehiclePanel.activeSelf, Is.False);
            Assert.That(upgradesPanel.activeSelf, Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(engineUpgrade.gameObject));

            controller.ShowNextTab();
            yield return null;
            Assert.That(controller.CurrentSection, Is.EqualTo(GarageSection.Vehicles));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(vehicleOption.gameObject));

            controller.ShowPreviousTab();
            yield return null;
            Assert.That(controller.CurrentSection, Is.EqualTo(GarageSection.Upgrades));

            vehiclesTab.onClick.Invoke();
            yield return null;
            Assert.That(controller.CurrentSection, Is.EqualTo(GarageSection.Vehicles));
            upgradesTab.onClick.Invoke();
            yield return null;
            Assert.That(controller.CurrentSection, Is.EqualTo(GarageSection.Upgrades));
        }

        [Test]
        public void GarageTabInputActions_UseQeAndGamepadShoulders()
        {
            var path = Path.Combine(Application.dataPath, "InputSystem_Actions.inputactions");
            var actions = InputActionAsset.FromJson(File.ReadAllText(path));
            var previous = actions.FindAction("UI/PreviousTab", true);
            var next = actions.FindAction("UI/NextTab", true);

            Assert.That(previous.bindings, Has.Some.Matches<InputBinding>(binding => binding.path == "<Keyboard>/q"));
            Assert.That(previous.bindings, Has.Some.Matches<InputBinding>(binding => binding.path == "<Gamepad>/leftShoulder"));
            Assert.That(next.bindings, Has.Some.Matches<InputBinding>(binding => binding.path == "<Keyboard>/e"));
            Assert.That(next.bindings, Has.Some.Matches<InputBinding>(binding => binding.path == "<Gamepad>/rightShoulder"));
        }

        [UnityTest]
        public IEnumerator PauseConfirmation_UsesActionSpecificMessage()
        {
            CreateEventSystem();
            var pauseRoot = new GameObject("Pause");
            pauseRoot.SetActive(false);
            var view = pauseRoot.AddComponent<PauseUguiView>();
            var retryButton = CreateButton("Retry");
            var exitButton = CreateButton("Exit");
            var confirmButton = CreateButton("Confirm");
            var cancelButton = CreateButton("Cancel");
            var dialog = new GameObject("ConfirmationDialog");
            dialog.SetActive(false);
            var message = new GameObject("ConfirmationText").AddComponent<Text>();
            message.transform.SetParent(dialog.transform, false);

            Set(view, "retryButton", retryButton);
            Set(view, "returnToMainMenuButton", exitButton);
            Set(view, "confirmationDialogRoot", dialog);
            Set(view, "confirmationText", message);
            Set(view, "confirmationConfirmButton", confirmButton);
            Set(view, "confirmationCancelButton", cancelButton);
            pauseRoot.SetActive(true);
            yield return null;

            retryButton.onClick.Invoke();
            Assert.That(message.text, Is.EqualTo("Restart the current race?"));

            cancelButton.onClick.Invoke();
            exitButton.onClick.Invoke();
            Assert.That(message.text, Is.EqualTo("Exit to the main menu? Race progress will be lost."));
        }

        [UnityTest]
        public IEnumerator MenuButtonVisualStateReservesRedFillForFocusedControl()
        {
            var eventSystem = CreateEventSystem();
            var primary = CreateButton("PrimaryButton");
            var currentTab = CreateButton("CurrentTab");
            var focused = CreateButton("FocusedButton");
            var primaryImage = primary.gameObject.AddComponent<Image>();
            var currentImage = currentTab.gameObject.AddComponent<Image>();
            var focusedImage = focused.gameObject.AddComponent<Image>();
            var primaryLabel = new GameObject("Label").AddComponent<Text>();
            primaryLabel.transform.SetParent(primary.transform, false);
            var primaryVisual = primary.gameObject.AddComponent<UguiMenuButtonVisualState>();
            var currentVisual = currentTab.gameObject.AddComponent<UguiMenuButtonVisualState>();
            var focusedVisual = focused.gameObject.AddComponent<UguiMenuButtonVisualState>();
            Set(primaryVisual, "button", primary);
            Set(primaryVisual, "background", primaryImage);
            Set(primaryVisual, "labels", new[] { primaryLabel });
            Set(currentVisual, "button", currentTab);
            Set(currentVisual, "background", currentImage);
            Set(focusedVisual, "button", focused);
            Set(focusedVisual, "background", focusedImage);
            primaryVisual.PrimaryAction = true;
            currentVisual.IsCurrent = true;

            yield return null;
            eventSystem.SetSelectedGameObject(focused.gameObject);
            yield return null;

            Assert.That(focusedImage.color, Is.EqualTo(ApexUiVisualStyle.PrimaryRed));
            Assert.That(primaryImage.color, Is.Not.EqualTo(ApexUiVisualStyle.PrimaryRed));
            Assert.That(currentImage.color, Is.Not.EqualTo(ApexUiVisualStyle.PrimaryRed));
            Assert.That(primaryLabel.color, Is.EqualTo(ApexUiVisualStyle.AccentText));

            eventSystem.SetSelectedGameObject(primary.gameObject);
            yield return null;

            Assert.That(primaryImage.color, Is.EqualTo(ApexUiVisualStyle.PrimaryRed));
            Assert.That(primaryLabel.color, Is.EqualTo(ApexUiVisualStyle.FocusedText));
            Assert.That(focusedImage.color, Is.Not.EqualTo(ApexUiVisualStyle.PrimaryRed));
        }

        [UnityTest]
        public IEnumerator SettingsNavigationSkipsMissingAudioTab()
        {
            CreateEventSystem();
            var root = new GameObject("Settings");
            root.SetActive(false);
            var view = root.AddComponent<SettingsUguiView>();
            var displayTab = CreateButton("DisplayTab");
            var displayPanel = new GameObject("DisplayPanel");
            var resolution = new GameObject("Resolution").AddComponent<SettingsOptionSelector>();
            var fullScreen = new GameObject("FullScreen").AddComponent<SettingsOptionSelector>();
            var restore = CreateButton("Restore");
            var cancel = CreateButton("Cancel");
            var apply = CreateButton("Apply");
            Set(view, "displayTabButton", displayTab);
            Set(view, "displayPanel", displayPanel);
            Set(view, "resolutionSelector", resolution);
            Set(view, "fullScreenModeSelector", fullScreen);
            Set(view, "restoreDefaultsButton", restore);
            Set(view, "cancelButton", cancel);
            Set(view, "applyButton", apply);

            root.SetActive(true);
            yield return null;

            Assert.That(displayTab.navigation.selectOnDown, Is.EqualTo(resolution));
            Assert.That(resolution.navigation.selectOnUp, Is.EqualTo(displayTab));
            Assert.That(resolution.navigation.selectOnDown, Is.EqualTo(fullScreen));
            Assert.That(restore.navigation.selectOnUp, Is.EqualTo(fullScreen));
        }

        [UnityTest]
        public IEnumerator HoverMovesFocusOnlyInMouseModality()
        {
            var eventSystem = CreateEventSystem();
            var first = CreateButton("FirstButton");
            var second = CreateButton("SecondButton");
            var secondVisual = second.gameObject.AddComponent<UguiMenuButtonVisualState>();
            Set(secondVisual, "button", second);
            Set(secondVisual, "background", second.gameObject.AddComponent<Image>());

            yield return null;
            eventSystem.SetSelectedGameObject(first.gameObject);

            UiInputModality.Set(UiInputMode.Navigation);
            secondVisual.OnPointerEnter(new PointerEventData(eventSystem));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(first.gameObject),
                "A resting cursor under keyboard/gamepad control must not steal focus.");

            UiInputModality.Set(UiInputMode.Mouse);
            secondVisual.OnPointerEnter(new PointerEventData(eventSystem));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(second.gameObject));
        }

        [UnityTest]
        public IEnumerator PointerActionsStayBoundAndCursorFollowsModality()
        {
            var previousVisibility = Cursor.visible;
            var previousLock = Cursor.lockState;
            var path = Path.Combine(Application.dataPath, "InputSystem_Actions.inputactions");
            var actions = InputActionAsset.FromJson(File.ReadAllText(path));
            var owner = new GameObject("UI Input Owner");
            owner.AddComponent<EventSystem>();
            var inputModule = owner.AddComponent<InputSystemUIInputModule>();
            var input = owner.AddComponent<BootstrapUiInputSystem>();
            Set(input, "inputActions", actions);
            Set(input, "inputModule", inputModule);
            Set(input, "allowPointerInput", true);
            var configure = typeof(BootstrapUiInputSystem).GetMethod("ConfigureInputModule", BindingFlags.Instance | BindingFlags.NonPublic);
            var setModality = typeof(BootstrapUiInputSystem).GetMethod("SetModality", BindingFlags.Instance | BindingFlags.NonPublic);
            configure.Invoke(input, null);
            yield return null;

            var point = inputModule.point;
            Assert.That(point, Is.Not.Null);

            setModality.Invoke(input, new object[] { UiInputMode.Navigation });
            yield return null;
            Assert.That(inputModule.point, Is.SameAs(point), "Navigation must not unbind the pointer.");
            Assert.That(point.action.enabled, Is.True, "Point must stay enabled so mouse movement can be detected again.");
            Assert.That(Cursor.visible, Is.False);

            setModality.Invoke(input, new object[] { UiInputMode.Mouse });
            yield return null;
            Assert.That(UiInputModality.Current, Is.EqualTo(UiInputMode.Mouse));
            Assert.That(Cursor.visible, Is.True);

            // A scene load must not rebuild (and thereby disable) the bound pointer actions.
            configure.Invoke(input, null);
            Assert.That(inputModule.point, Is.SameAs(point));
            Assert.That(point.action.enabled, Is.True);

            Object.Destroy(owner);
            Object.Destroy(actions);
            Cursor.visible = previousVisibility;
            Cursor.lockState = previousLock;
        }

        [Test]
        public void BootstrapTabActionNamesExistInUiMap()
        {
            var path = Path.Combine(Application.dataPath, "InputSystem_Actions.inputactions");
            var actions = InputActionAsset.FromJson(File.ReadAllText(path));
            var uiMap = actions.FindActionMap("UI", true);

            Assert.That(uiMap.FindAction(BootstrapUiInputSystem.PreviousTabActionName, false), Is.Not.Null);
            Assert.That(uiMap.FindAction(BootstrapUiInputSystem.NextTabActionName, false), Is.Not.Null);
        }

        private static EventSystem CreateEventSystem()
        {
            var existing = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < existing.Length; index++)
            {
                Object.DestroyImmediate(existing[index].gameObject);
            }

            var navigationControllers = Object.FindObjectsByType<UguiMainMenuNavigationController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < navigationControllers.Length; index++)
            {
                Object.DestroyImmediate(navigationControllers[index].gameObject);
            }

            var eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            return eventSystem;
        }

        private static Button CreateButton(string name)
        {
            var button = new GameObject(name).AddComponent<Button>();
            return button;
        }

        private static VehiclePerformanceStats CreateStats()
        {
            return new VehiclePerformanceStats(40f, 30f, 90f, 0.8f, 0.4f);
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
