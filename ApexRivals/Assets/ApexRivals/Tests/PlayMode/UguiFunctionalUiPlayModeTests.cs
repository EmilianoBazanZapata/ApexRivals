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
            var eventSystem = CreateEventSystem();
            var play = CreateButton("PlayButton");
            var garage = CreateButton("GarageButton");
            var controller = new GameObject("MainMenuNavigation").AddComponent<UguiMainMenuNavigationController>();
            Set(controller, "playButton", play);
            Set(controller, "garageButton", garage);

            yield return null;
            controller.RefreshNavigation();
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(play.gameObject));

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

            Assert.That(image.color.r, Is.LessThan(0.25f));
            Assert.That(label.color.r, Is.LessThan(0.7f));
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
