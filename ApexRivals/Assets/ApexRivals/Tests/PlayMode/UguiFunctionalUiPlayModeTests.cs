using System.Collections;
using System.Reflection;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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

            navigator.Show(PresentationScreenState.Settings);
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
                new GarageUpgradeViewModel(UpgradeType.Engine, 3, 3, 0, false),
                new GarageUpgradeViewModel(UpgradeType.Handling, 0, 3, 200, false),
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

        private static EventSystem CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            return eventSystem;
        }

        private static ArcadeVehicleStats CreateStats()
        {
            return new ArcadeVehicleStats(40f, 20f, 60f, 30f, 10f, 90f, 0.2f, 1.2f, 0.8f, 0.4f, 30f, 0.75f, -1, 0.75f, 0.5f);
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
