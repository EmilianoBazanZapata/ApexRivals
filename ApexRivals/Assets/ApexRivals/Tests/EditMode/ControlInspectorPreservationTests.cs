using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using ApexRivals.Input.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ApexRivals.Tests.EditMode.ControlInspector
{
    public sealed class ControlInspectorPreservationTests
    {
        private GameObject _root;
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<InputActionMap> _maps = new List<InputActionMap>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ControlInspectorTest");
            _root.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            foreach (var item in _owned) if (item != null) Object.DestroyImmediate(item);
            foreach (var map in _maps) map.Dispose();
            _owned.Clear();
            _maps.Clear();
        }

        [Test]
        public void ControllerKeepsAuthoredRigidbodyModesCenterOfMassAndInertia()
        {
            var body = _root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Extrapolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0.2f, -0.7f, 0.3f);
            body.inertiaTensor = new Vector3(10, 20, 30);
            body.inertiaTensorRotation = Quaternion.Euler(10, 20, 30);
            var expectedRotation = body.inertiaTensorRotation;
            var configuration = ScriptableObject.CreateInstance<WheelVehicleConfiguration>();
            _owned.Add(configuration);
            var controller = _root.AddComponent<WheelArcadeVehicleController>();
            Set(controller, "configuration", configuration);
            Invoke(controller, "Awake");
            Assert.That(body.useGravity, Is.False);
            Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Extrapolate));
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousSpeculative));
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationZ));
            Assert.That(body.centerOfMass, Is.EqualTo(new Vector3(0.2f, -0.7f, 0.3f)));
            Assert.That(Vector3.Distance(body.inertiaTensor, new Vector3(10, 20, 30)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(body.inertiaTensorRotation, expectedRotation), Is.LessThan(0.01f));
            Assert.That(body.automaticCenterOfMass, Is.False);
            Assert.That(body.automaticInertiaTensor, Is.False);
            Assert.That(body.mass, Is.EqualTo(configuration.mass), "Explicit configuration tuning must still apply.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChassisUsesTheSelectedCenterOfMassOwner(bool useReference)
        {
            var body = _root.AddComponent<Rigidbody>();
            body.centerOfMass = new Vector3(0.1f, -0.2f, 0.3f);
            var reference = Child("MassReference").transform;
            reference.localPosition = new Vector3(0.4f, -0.6f, 0.8f);
            var stability = _root.AddComponent<VehicleChassisStability>();
            Set(stability, "centerOfMassReference", reference);
            Set(stability, "useCenterOfMassReference", useReference);
            Invoke(stability, "Awake");
            Invoke(stability, "FixedUpdate");
            Assert.That(body.centerOfMass, Is.EqualTo(useReference ? reference.localPosition : new Vector3(0.1f, -0.2f, 0.3f)));
        }

        [TestCase(typeof(UguiMainMenuNavigationController), "playButton", "RefreshNavigation", false)]
        [TestCase(typeof(GarageUguiView), "previousVehicleButton", "ConfigureNavigation", false)]
        [TestCase(typeof(GarageUguiView), "engineTileButton", "ConfigurePartsNavigation", false)]
        [TestCase(typeof(PauseUguiView), "resumeButton", "ConfigurePauseNavigation", false)]
        [TestCase(typeof(PauseUguiView), "confirmationConfirmButton", "ConfigureConfirmationNavigation", false)]
        [TestCase(typeof(SettingsUguiView), "displayTabButton", "ConfigureNavigation", false)]
        [TestCase(typeof(TrackSelectionUguiView), "previousTrackButton", "ConfigureNavigation", true)]
        public void AuthoredNavigationKeepsModeWrappingAndDirectionalLinks(Type type, string field, string method, bool hasIndex)
        {
            var component = _root.AddComponent(type);
            var button = Child("Button").AddComponent<Button>();
            var other = Child("Other").AddComponent<Button>();
            var navigation = new Navigation { mode = Navigation.Mode.Explicit, wrapAround = true,
                selectOnUp = other, selectOnDown = other, selectOnLeft = other, selectOnRight = other };
            button.navigation = navigation;
            Set(component, "useDynamicNavigation", false);
            Set(component, field, button);
            if (hasIndex) Invoke(component, method, 0); else Invoke(component, method);
            Assert.That(button.navigation.mode, Is.EqualTo(navigation.mode));
            Assert.That(button.navigation.wrapAround, Is.True);
            Assert.That(button.navigation.selectOnUp, Is.EqualTo(other));
            Assert.That(button.navigation.selectOnDown, Is.EqualTo(other));
            Assert.That(button.navigation.selectOnLeft, Is.EqualTo(other));
            Assert.That(button.navigation.selectOnRight, Is.EqualTo(other));
        }

        [Test]
        public void AuthoredUiModuleKeepsItsAssetReferencesPointerAndRepeatSettings()
        {
            var bootstrap = _root.AddComponent<BootstrapUiInputSystem>();
            var module = _root.GetComponent<InputSystemUIInputModule>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _owned.Add(asset);
            var ui = asset.AddActionMap("UI");
            var move = Reference(ui.AddAction("CustomMove", InputActionType.PassThrough, expectedControlLayout: "Vector2"));
            var submit = Reference(ui.AddAction("CustomSubmit", InputActionType.Button));
            var cancel = Reference(ui.AddAction("CustomCancel", InputActionType.Button));
            var point = Reference(ui.AddAction("CustomPoint", InputActionType.PassThrough, expectedControlLayout: "Vector2"));
            var click = Reference(ui.AddAction("CustomClick", InputActionType.Button));
            module.actionsAsset = asset;
            module.move = move;
            module.submit = submit;
            module.cancel = cancel;
            module.point = point;
            module.leftClick = click;
            module.moveRepeatDelay = 0.73f;
            module.moveRepeatRate = 0.19f;
            Set(bootstrap, "inputModule", module);
            Set(bootstrap, "configureModuleFromBootstrap", false);
            Set(bootstrap, "allowPointerInput", false); // Legacy-only field must not erase authored pointer references.
            var before = asset.ToJson();
            Invoke(bootstrap, "ConfigureInputModule");
            Invoke(bootstrap, "ConfigureInputModule");
            Assert.That(module.actionsAsset, Is.EqualTo(asset));
            Assert.That(module.move, Is.EqualTo(move));
            Assert.That(module.submit, Is.EqualTo(submit));
            Assert.That(module.cancel, Is.EqualTo(cancel));
            Assert.That(module.point, Is.EqualTo(point));
            Assert.That(module.leftClick, Is.EqualTo(click));
            Assert.That(module.moveRepeatDelay, Is.EqualTo(0.73f));
            Assert.That(module.moveRepeatRate, Is.EqualTo(0.19f));
            Assert.That(Get(bootstrap, "_submitAction"), Is.EqualTo(submit.action));
            Assert.That(Get(bootstrap, "_cancelAction"), Is.EqualTo(cancel.action));
            Assert.That(asset.ToJson(), Is.EqualTo(before));
        }

        [Test]
        public void UnknownInputOwnersAreReportedWithoutDisablingTheirComponentsOrGameObjects()
        {
            var bootstrap = _root.AddComponent<BootstrapUiInputSystem>();
            Set(bootstrap, "eventSystem", _root.GetComponent<EventSystem>());
            Set(bootstrap, "inputModule", _root.GetComponent<InputSystemUIInputModule>());
            var peer = new GameObject("PeerInput");
            _owned.Add(peer);
            var system = peer.AddComponent<EventSystem>();
            var module = peer.AddComponent<InputSystemUIInputModule>();
            LogAssert.Expect(LogType.Warning, new Regex("Additional UI input owner 'PeerInput'"));
            LogAssert.Expect(LogType.Warning, new Regex("Additional UI input owner 'PeerInput'"));
            Invoke(bootstrap, "DisableCompetingComponents", peer);
            Assert.That(peer.activeSelf, Is.True);
            Assert.That(system.enabled, Is.True);
            Assert.That(module.enabled, Is.True);
            Invoke(bootstrap, "DisableCompetingComponents", peer); // No repeated warning spam.
        }

        [Test]
        public void DrivingInputClonesAuthoredBindingsProcessorsAndOverridesPerPlayer()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _owned.Add(asset);
            var map = asset.AddActionMap("CustomDriving");
            foreach (var name in new[] { "Throttle", "Brake", "Steering", "Drift", "Nitro", "Reset", "RecoverVehicle" })
                map.AddAction(name, name == "Throttle" || name == "Brake" || name == "Steering" ? InputActionType.Value : InputActionType.Button).AddBinding("<Keyboard>/g");
            map.FindAction("Throttle").ApplyBindingOverride(0, "<Keyboard>/f");
            var input = _root.AddComponent<PlayerDrivingInput>();
            Set(input, "inputActions", asset);
            Set(input, "drivingActionMap", "CustomDriving");
            var before = asset.ToJson();
            Invoke(input, "Awake");
            var clone = (InputActionMap)Get(input, "_drivingActions");
            _maps.Add(clone);
            Assert.That(clone, Is.Not.SameAs(map));
            Assert.That(clone.FindAction("Throttle").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f"));
            clone.FindAction("Throttle").ApplyBindingOverride(0, "<Keyboard>/h");
            Assert.That(map.FindAction("Throttle").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f"));
            Assert.That(asset.ToJson(), Is.EqualTo(before));
            Set(input, "_drivingActions", null);
        }

        [Test]
        public void ConfiguredMissingDrivingMapDoesNotSilentlyReplaceItWithFixedBindings()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _owned.Add(asset);
            var input = _root.AddComponent<PlayerDrivingInput>();
            Set(input, "inputActions", asset);
            Set(input, "drivingActionMap", "Missing");
            LogAssert.Expect(LogType.Error, "Driving action map missing: Missing");
            Invoke(input, "Awake");
            Assert.That(Get(input, "_drivingActions"), Is.Null);
            Invoke(input, "Update");
            Assert.That(input.CurrentInput.Throttle, Is.Zero);
        }

        [Test]
        public void PauseActionUsesTheAuthoredBindingWithoutEditingTheSharedAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _owned.Add(asset);
            var action = asset.AddActionMap("Custom").AddAction("Menu", InputActionType.Button);
            action.AddBinding("<Keyboard>/p");
            var before = asset.ToJson();
            var installer = _root.AddComponent<RaceUiSceneInstaller>();
            Set(installer, "sessionInputActions", asset);
            Set(installer, "pauseActionPath", "Custom/Menu");
            Invoke(installer, "CreatePauseAction");
            var clone = (InputAction)Get(installer, "_pauseAction");
            Assert.That(clone.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/p"));
            Assert.That(clone, Is.Not.SameAs(action));
            Assert.That(action.enabled, Is.False);
            Assert.That(asset.ToJson(), Is.EqualTo(before));
            Invoke(installer, "ReleasePauseAction");
        }

        [Test]
        public void CancelRoleFollowsTheAuthoredBindingInsteadOfTheEscapeKey()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _owned.Add(asset);
            var cancel = asset.AddActionMap("UI").AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/p");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                asset.devices = new InputDevice[] { keyboard };
                var bootstrap = _root.AddComponent<BootstrapUiInputSystem>();
                Set(bootstrap, "_cancelAction", cancel);
                Assert.That(bootstrap.CanHandleCancelInput, Is.False, "An inactive owner must not claim runtime cancel dispatch.");
                Assert.That(bootstrap.IsCancelControl(keyboard.pKey), Is.True);
                Assert.That(bootstrap.IsCancelControl(keyboard.escapeKey), Is.False);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [Test]
        public void CancelDoesNotImmediatelyCloseAPausePanelOpenedByTheSameInputFrame()
        {
            var view = _root.AddComponent<PauseUguiView>();
            var resumes = 0;
            view.ResumeRequested += () => resumes++;
            view.SuppressCancelForCurrentFrame();
            view.OnCancel(new BaseEventData(null));
            Assert.That(resumes, Is.Zero);
            Set(view, "_suppressCancelFrame", -1);
            view.OnCancel(new BaseEventData(null));
            Assert.That(resumes, Is.EqualTo(1));
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            return child;
        }
        private InputActionReference Reference(InputAction action)
        {
            var reference = InputActionReference.Create(action);
            _owned.Add(reference);
            return reference;
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    }
}
