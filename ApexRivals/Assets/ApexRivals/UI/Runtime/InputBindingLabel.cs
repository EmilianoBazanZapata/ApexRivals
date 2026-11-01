using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Which device's bindings an <see cref="InputBindingLabel"/> lists.</summary>
    public enum InputBindingDevice
    {
        Keyboard,
        Gamepad
    }

    /// <summary>
    /// Lists one action's bindings for one device, read from the authored Input Action Asset ("W, ↑", "RT"), so the
    /// controls screen always matches the real configuration. Composite parts are joined ("A / D"); separate bindings
    /// are listed with the separator. Gamepad controls use Xbox names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputBindingLabel : MonoBehaviour
    {
        private static readonly Dictionary<string, string> GamepadNames = new Dictionary<string, string>
        {
            { "buttonSouth", "A" },
            { "buttonEast", "B" },
            { "buttonWest", "X" },
            { "buttonNorth", "Y" },
            { "leftTrigger", "LT" },
            { "rightTrigger", "RT" },
            { "leftShoulder", "LB" },
            { "rightShoulder", "RB" },
            { "start", "Menu" },
            { "select", "View" },
            { "leftStick", "Left Stick" },
            { "leftStick/x", "Left Stick" },
            { "leftStick/y", "Left Stick" },
            { "rightStick", "Right Stick" },
            { "dpad", "D-Pad" }
        };

        private static readonly Dictionary<string, string> KeyboardNames = new Dictionary<string, string>
        {
            { "upArrow", "↑" },
            { "downArrow", "↓" },
            { "leftArrow", "←" },
            { "rightArrow", "→" },
            { "leftShift", "Shift" },
            { "rightShift", "Shift" },
            { "escape", "Esc" }
        };

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField, Tooltip("Map/Action, for example Driving/Throttle.")] private string actionPath = "Driving/Throttle";
        [SerializeField] private InputBindingDevice device;
        [SerializeField] private Text label;
        [SerializeField, Tooltip("Between separate bindings of the same action.")] private string separator = ", ";
        [SerializeField, Tooltip("Between the parts of one composite binding, for example A / D.")] private string compositeSeparator = " / ";
        [SerializeField, Tooltip("Shown when the action has no binding for this device.")] private string unboundText = "-";

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var action = inputActions != null ? inputActions.FindAction(actionPath, false) : null;
            UguiViewText.Set(label, action != null ? Describe(action, device, separator, compositeSeparator, unboundText) : unboundText);
        }

        public static string Describe(InputAction action, InputBindingDevice device, string separator, string compositeSeparator, string unboundText)
        {
            var entries = new List<string>();
            var bindings = action.bindings;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.isComposite)
                {
                    var parts = new List<string>();
                    var part = index + 1;
                    for (; part < bindings.Count && bindings[part].isPartOfComposite; part++)
                    {
                        if (Matches(bindings[part].effectivePath, device)) parts.Add(ControlName(bindings[part].effectivePath, device));
                    }

                    if (parts.Count > 0) entries.Add(string.Join(compositeSeparator, parts));
                    index = part - 1;
                }
                else if (!binding.isPartOfComposite && Matches(binding.effectivePath, device))
                {
                    var name = ControlName(binding.effectivePath, device);
                    if (!entries.Contains(name)) entries.Add(name);
                }
            }

            return entries.Count > 0 ? string.Join(separator, entries) : unboundText;
        }

        private static bool Matches(string path, InputBindingDevice device)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return device == InputBindingDevice.Keyboard
                ? path.StartsWith("<Keyboard>")
                : path.StartsWith("<Gamepad>");
        }

        private static string ControlName(string path, InputBindingDevice device)
        {
            var control = path.Substring(path.IndexOf('>') + 1).TrimStart('/');
            var names = device == InputBindingDevice.Gamepad ? GamepadNames : KeyboardNames;
            if (names.TryGetValue(control, out var name)) return name;
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
    }
}
