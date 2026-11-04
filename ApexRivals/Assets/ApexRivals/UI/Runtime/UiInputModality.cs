using System;

namespace ApexRivals.UI.Runtime
{
    public enum UiInputMode
    {
        Navigation,
        Mouse
    }

    /// <summary>
    /// Device family the player is currently driving the UI with. BootstrapUiInputSystem owns the
    /// transitions; controls read it so a hidden, resting cursor cannot steal keyboard/gamepad focus.
    /// </summary>
    public static class UiInputModality
    {
        public static UiInputMode Current { get; private set; } = UiInputMode.Navigation;

        public static bool IsMouse => Current == UiInputMode.Mouse;

        public static event Action<UiInputMode> Changed;

        public static void Set(UiInputMode mode)
        {
            if (Current == mode)
            {
                return;
            }

            Current = mode;
            Changed?.Invoke(mode);
        }
    }
}
