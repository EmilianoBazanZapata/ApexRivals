using System.Collections;
using ApexRivals.Input.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class PlayerNitroInputPlayModeTests
    {
        [UnityTest]
        public IEnumerator NitroKeyboardAndGamepadRespectTheDrivingGate()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var owner = new GameObject("NitroInputTest");
            try
            {
                var input = owner.AddComponent<PlayerDrivingInput>();
                var gate = owner.AddComponent<DrivingInputGate>();
                gate.Configure(input, true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                InputSystem.Update();
                yield return null;
                Assert.That(gate.CurrentInput.Nitro, Is.True);
                gate.SetDrivingAllowed(false);
                Assert.That(gate.CurrentInput.Nitro, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.West));
                InputSystem.Update();
                gate.SetDrivingAllowed(true);
                yield return null;
                Assert.That(gate.CurrentInput.Nitro, Is.True);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update();
                yield return null;
                Assert.That(gate.CurrentInput.Nitro, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                InputSystem.RemoveDevice(gamepad);
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
