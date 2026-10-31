#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator SharedAuthoredPauseAndCancelBindingDoesNotToggleTwice()
        {
            var ui = InputActionAsset.FromJson(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions").ToJson());
            var sessionControls = InputActionAsset.FromJson(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ApexRivals/Input/ApexDriving.inputactions").ToJson());
            var cancel = ui.FindAction("UI/Cancel", true);
            for (var i = 0; i < cancel.bindings.Count; i++) cancel.ApplyBindingOverride(i, new InputBinding { overridePath = string.Empty });
            cancel.ApplyBindingOverride(0, "<Keyboard>/p");
            sessionControls.FindAction("Session/Pause", true).ApplyBindingOverride(0, "<Keyboard>/p");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                ui.devices = new InputDevice[] { keyboard };
                sessionControls.devices = new InputDevice[] { keyboard };
                var owner = _bootstrapper.gameObject.AddComponent<BootstrapUiInputSystem>();
                var module = owner.GetComponent<InputSystemUIInputModule>();
                module.actionsAsset = ui;
                module.submit = null;
                module.cancel = null;
                owner.SendMessage("ConfigureInputModule");
                yield return WaitForTask(_flow.RetryRace());
                var installer = FindInRace<RaceUiSceneInstaller>();
                Assert.That(installer, Is.Not.Null);
                typeof(RaceUiSceneInstaller).GetField("sessionInputActions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(installer, sessionControls);
                installer.SendMessage("ReleasePauseAction");
                installer.SendMessage("CreatePauseAction");
                var previous = Installer.Session.State;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P));
                InputSystem.Update();
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Paused));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return new WaitForSecondsRealtime(0.25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P));
                InputSystem.Update();
                Assert.That(Installer.Session.State, Is.EqualTo(previous), "Shared P must resume once, not immediately pause again.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
            }
            finally
            {
                var owner = _bootstrapper != null ? _bootstrapper.GetComponent<BootstrapUiInputSystem>() : null;
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                InputSystem.RemoveDevice(keyboard);
                UnityEngine.Object.DestroyImmediate(ui);
                UnityEngine.Object.DestroyImmediate(sessionControls);
            }
        }
    }
}
#endif
