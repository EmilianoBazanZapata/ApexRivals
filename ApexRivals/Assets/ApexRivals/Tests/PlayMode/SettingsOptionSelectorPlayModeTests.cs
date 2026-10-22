using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ApexRivals.UI.Runtime;
using ApexRivals.Settings.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class SettingsOptionSelectorPlayModeTests
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
        public IEnumerator ResolvesNamedChildTextsWhenInspectorReferencesAreEmpty()
        {
            var root = new GameObject("Resolution", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var selector = root.AddComponent<SettingsOptionSelector>();
            var value = CreateTextTarget("Value", (RectTransform)root.transform, Vector2.zero);
            var previous = CreateTextTarget("PreviousArrow", (RectTransform)root.transform, new Vector2(-236f, 0f));
            var next = CreateTextTarget("NextArrow", (RectTransform)root.transform, new Vector2(236f, 0f));
            selector.SetOptions(new[] { "1600 x 1024", "1920 x 1080" }, 0);
            yield return null;

            Assert.That(value.GetComponent<Text>().text, Is.EqualTo("1600 x 1024"));
            Assert.That(previous.GetComponent<Text>().text, Is.EqualTo("<"));
            Assert.That(next.GetComponent<Text>().text, Is.EqualTo(">"));
        }

        [UnityTest]
        public IEnumerator CenterClickSelectsRowWithoutChangingValue()
        {
            var eventSystem = CreateEventSystem();
            var selector = CreateSelector("Resolution");
            var changedCount = 0;
            selector.ValueChanged += _ => changedCount++;
            yield return null;

            var eventData = PointerAt(eventSystem, selector.transform.position);
            selector.OnPointerClick(eventData);

            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(selector.gameObject));
            Assert.That(selector.SelectedIndex, Is.EqualTo(0));
            Assert.That(changedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ArrowClicksChangeOneStepAndWrapAround()
        {
            var eventSystem = CreateEventSystem();
            var selector = CreateSelector("Resolution");
            var previous = (RectTransform)Get(selector, "previousHitTarget");
            var next = (RectTransform)Get(selector, "nextHitTarget");
            yield return null;

            selector.OnPointerClick(PointerAt(eventSystem, next.position));
            Assert.That(selector.SelectedIndex, Is.EqualTo(1));

            selector.OnPointerClick(PointerAt(eventSystem, previous.position));
            Assert.That(selector.SelectedIndex, Is.EqualTo(0));

            selector.OnPointerClick(PointerAt(eventSystem, previous.position));
            Assert.That(selector.SelectedIndex, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator HorizontalMoveChangesIndexAndRaisesValueChangedOnce()
        {
            var eventSystem = CreateEventSystem();
            var selector = CreateSelector("Resolution");
            var changedCount = 0;
            var changedIndex = -1;
            selector.ValueChanged += index =>
            {
                changedCount++;
                changedIndex = index;
            };
            yield return null;

            selector.OnMove(MoveEvent(eventSystem, Vector2.right, MoveDirection.Right));
            Assert.That(selector.SelectedIndex, Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(changedIndex, Is.EqualTo(1));

            selector.OnMove(MoveEvent(eventSystem, Vector2.left, MoveDirection.Left));
            Assert.That(selector.SelectedIndex, Is.EqualTo(0));
            Assert.That(changedCount, Is.EqualTo(2));
            Assert.That(changedIndex, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SelectionHoverAndDeselectionDoNotChangeValue()
        {
            var eventSystem = CreateEventSystem();
            var selectorA = CreateSelector("RowA");
            var selectorB = CreateSelector("RowB");
            var changedCount = 0;
            selectorA.ValueChanged += _ => changedCount++;
            selectorB.ValueChanged += _ => changedCount++;
            yield return null;

            eventSystem.SetSelectedGameObject(selectorA.gameObject);
            UiInputModality.Set(UiInputMode.Mouse);
            selectorB.OnPointerEnter(PointerAt(eventSystem, selectorB.transform.position));

            Assert.That(changedCount, Is.Zero);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(selectorB.gameObject));
            Assert.That(selectorA.GetComponent<Image>().color, Is.EqualTo(ApexUiVisualStyle.PanelElevated));
            Assert.That(selectorB.GetComponent<Image>().color, Is.EqualTo(ApexUiVisualStyle.PrimaryRed));
        }

        [UnityTest]
        public IEnumerator NonInteractableSelectorIgnoresClicksAndMovesAndStaysDisabledAfterSetOptions()
        {
            var eventSystem = CreateEventSystem();
            var selector = CreateSelector("RefreshRate");
            var next = (RectTransform)Get(selector, "nextHitTarget");
            yield return null;
            selector.interactable = false;

            selector.OnPointerClick(PointerAt(eventSystem, next.position));
            selector.OnMove(MoveEvent(eventSystem, Vector2.right, MoveDirection.Right));
            selector.SetOptions(new[] { "Desktop" }, 0);

            Assert.That(selector.SelectedIndex, Is.Zero);
            Assert.That(selector.IsInteractable(), Is.False);
        }

        [UnityTest]
        public IEnumerator AudioRendersKeepDisplayLabelsAndSelection()
        {
            var view = new GameObject("Settings").AddComponent<SettingsUguiView>();
            var resolution = CreateSelector("Resolution");
            Set(view, "resolutionSelector", resolution);
            var modes = new[] { new SettingsResolution(1280, 720, 60), new SettingsResolution(1920, 1080, 144) };
            view.Render(Model(modes, modes[1], FullScreenMode.ExclusiveFullScreen, 0.5f));
            var options = (List<string>)Get(resolution, "_options");
            var firstLabel = options[0];

            view.Render(Model(modes, modes[1], FullScreenMode.ExclusiveFullScreen, 0.2f));
            yield return null;

            Assert.That(options[0], Is.SameAs(firstLabel), "Audio changes must not rebuild selector labels.");
            Assert.That(resolution.SelectedIndex, Is.EqualTo(1));
            Assert.That(resolution.OptionCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ChangedModesAndBorderlessStateInvalidateDisplayOptions()
        {
            var events = CreateEventSystem();
            var view = new GameObject("Settings").AddComponent<SettingsUguiView>();
            var resolution = CreateSelector("Resolution");
            var refresh = CreateSelector("Refresh");
            var fullscreen = CreateSelector("Fullscreen");
            Set(view, "resolutionSelector", resolution);
            Set(view, "refreshRateSelector", refresh);
            Set(view, "fullScreenModeSelector", fullscreen);
            var modes = new List<SettingsResolution> { new SettingsResolution(1920, 1080, 60), new SettingsResolution(1920, 1080, 144) };
            view.Render(Model(modes, modes[1], FullScreenMode.ExclusiveFullScreen, 0.5f));
            Assert.That(refresh.OptionCount, Is.EqualTo(2));
            Assert.That(refresh.SelectedIndex, Is.EqualTo(1));
            UiFocus.Request(refresh);

            view.Render(Model(modes, modes[1], FullScreenMode.FullScreenWindow, 0.5f));
            Assert.That(refresh.IsInteractable(), Is.False);
            Assert.That(refresh.OptionCount, Is.EqualTo(1));
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(fullscreen.gameObject));
            modes.Add(new SettingsResolution(2560, 1440, 165));
            view.Render(Model(modes, modes[2], FullScreenMode.Windowed, 0.5f));
            yield return null;

            Assert.That(resolution.OptionCount, Is.EqualTo(2));
            Assert.That(resolution.SelectedIndex, Is.EqualTo(1));
            Assert.That(refresh.IsInteractable(), Is.False, "Windowed mode also uses the desktop refresh rate.");
            Assert.That(refresh.OptionCount, Is.EqualTo(1));
            view.Render(Model(modes, modes[2], FullScreenMode.ExclusiveFullScreen, 0.5f));
            Assert.That(refresh.IsInteractable(), Is.True);
        }

        private static SettingsViewModel Model(IReadOnlyList<SettingsResolution> modes, SettingsResolution selected,
            FullScreenMode fullscreen, float volume)
        {
            return new SettingsViewModel(modes, selected, fullscreen, volume, 0.5f, 0.5f,
                true, true, PresentationFailure.None, string.Empty);
        }

        private static EventSystem CreateEventSystem()
        {
            var existing = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < existing.Length; index++)
            {
                Object.DestroyImmediate(existing[index].gameObject);
            }

            return new GameObject("EventSystem").AddComponent<EventSystem>();
        }

        private static SettingsOptionSelector CreateSelector(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(520f, 58f);
            var selector = root.AddComponent<SettingsOptionSelector>();
            var previous = CreateTextTarget("PreviousArrow", rootRect, new Vector2(-236f, 0f));
            var value = CreateTextTarget("Value", rootRect, Vector2.zero);
            value.anchorMin = new Vector2(0.1f, 0f);
            value.anchorMax = new Vector2(0.9f, 1f);
            value.sizeDelta = Vector2.zero;
            var next = CreateTextTarget("NextArrow", rootRect, new Vector2(236f, 0f));

            var previousText = previous.GetComponent<Text>();
            var valueText = value.GetComponent<Text>();
            var nextText = next.GetComponent<Text>();
            Set(selector, "background", root.GetComponent<Image>());
            Set(selector, "previousArrowText", previousText);
            Set(selector, "valueText", valueText);
            Set(selector, "nextArrowText", nextText);
            Set(selector, "previousHitTarget", previous);
            Set(selector, "nextHitTarget", next);
            selector.SetOptions(new List<string> { "First", "Second", "Third" }, 0);
            return selector;
        }

        private static RectTransform CreateTextTarget(string name, RectTransform parent, Vector2 anchoredPosition)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)target.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(48f, 58f);
            target.GetComponent<Text>().raycastTarget = false;
            return rect;
        }

        private static PointerEventData PointerAt(EventSystem eventSystem, Vector3 worldPosition)
        {
            return new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, worldPosition)
            };
        }

        private static AxisEventData MoveEvent(EventSystem eventSystem, Vector2 moveVector, MoveDirection direction)
        {
            return new AxisEventData(eventSystem)
            {
                moveVector = moveVector,
                moveDir = direction
            };
        }

        private static object Get(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void Set(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
