#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ApexRivals.Input.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class ControlInspectorPlayModeTests
    {
        private readonly List<Behaviour> _disabled = new List<Behaviour>();

        [SetUp]
        public void SetUp()
        {
            // Preserve any pre-existing test-host input owners while isolating real device events.
            foreach (var owner in Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (owner.enabled && (owner is EventSystem || owner is InputSystemUIInputModule || owner is BootstrapUiInputSystem))
                {
                    _disabled.Add(owner);
                    owner.enabled = false;
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var owner in _disabled) if (owner != null) owner.enabled = true;
            _disabled.Clear();
        }

        [UnityTest]
        public IEnumerator AuthoredDrivingBindingsReadKeyboardAndGamepadWithoutMutatingTheAsset()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ApexRivals/Input/ApexDriving.inputactions");
            Assert.That(source, Is.Not.Null);
            var sourceJson = source.ToJson();
            var authored = InputActionAsset.FromJson(sourceJson);
            authored.FindAction("Driving/Throttle").ApplyBindingOverride(0, "<Keyboard>/f");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            authored.devices = new InputDevice[] { keyboard, gamepad };
            var owner = new GameObject("AuthoredDrivingTest");
            owner.SetActive(false);
            try
            {
                var input = owner.AddComponent<PlayerDrivingInput>();
                Set(input, "inputActions", authored);
                owner.SetActive(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F, Key.LeftShift));
                InputSystem.Update();
                yield return null;
                Assert.That(input.CurrentInput.Throttle, Is.GreaterThan(0.9f));
                Assert.That(input.CurrentInput.Nitro, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                yield return null;
                Assert.That(input.CurrentInput.Throttle, Is.Zero, "The edited F binding must not be replaced with W.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 0.8f, leftStick = new Vector2(0.5f, 0) }.WithButton(GamepadButton.West));
                InputSystem.Update();
                yield return null;
                Assert.That(input.CurrentInput.Throttle, Is.GreaterThan(0.5f));
                Assert.That(input.CurrentInput.Steering, Is.GreaterThan(0.2f));
                Assert.That(input.CurrentInput.Nitro, Is.True);
                owner.SetActive(false);
                Assert.That(input.CurrentInput.Throttle, Is.Zero);
                Assert.That(source.ToJson(), Is.EqualTo(sourceJson));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(authored);
                InputSystem.RemoveDevice(gamepad);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator NativeModuleSubmitIsDispatchedOnceAndKeepsAuthoredReferences()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("UI");
            var move = map.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            var submit = map.AddAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/f");
            var cancel = map.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/g");
            var moveReference = InputActionReference.Create(move);
            var submitReference = InputActionReference.Create(submit);
            var cancelReference = InputActionReference.Create(cancel);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            asset.devices = new InputDevice[] { keyboard };
            var owner = new GameObject("AuthoredUiTest");
            owner.SetActive(false);
            try
            {
                var bootstrap = owner.AddComponent<BootstrapUiInputSystem>();
                var system = owner.GetComponent<EventSystem>();
                var module = owner.GetComponent<InputSystemUIInputModule>();
                module.actionsAsset = asset;
                module.move = moveReference;
                module.submit = submitReference;
                module.cancel = cancelReference;
                module.moveRepeatDelay = 0.73f;
                module.moveRepeatRate = 0.19f;
                var target = new GameObject("SubmitTarget", typeof(RectTransform), typeof(Button));
                target.transform.SetParent(owner.transform, false);
                var button = target.GetComponent<Button>();
                var submits = 0;
                button.onClick.AddListener(() => submits++);
                owner.SetActive(true);
                EventSystem.current = system;
                system.SetSelectedGameObject(target);
                yield return null;
                owner.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
                Assert.That(module.submit, Is.EqualTo(submitReference));
                Assert.That(submit.enabled, Is.True);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(target));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                InputSystem.Update();
                Assert.That(submit.IsPressed(), Is.True);
                // Process the same dynamic update as the event. A second manual InputSystem
                // update followed by a normal player-loop update would clear WasPerformed.
                module.Process();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                Assert.That(submits, Is.EqualTo(1));
                Assert.That(module.submit, Is.EqualTo(submitReference));
                Assert.That(module.cancel, Is.EqualTo(cancelReference));
                Assert.That(module.moveRepeatDelay, Is.EqualTo(0.73f));
                Assert.That(module.moveRepeatRate, Is.EqualTo(0.19f));
                Assert.That(bootstrap.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(moveReference);
                Object.DestroyImmediate(submitReference);
                Object.DestroyImmediate(cancelReference);
                Object.DestroyImmediate(asset);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif
