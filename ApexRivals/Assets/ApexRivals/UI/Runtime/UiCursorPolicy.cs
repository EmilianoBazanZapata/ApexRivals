using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public static class UiCursorPolicy
    {
        /// <summary>
        /// Menus show the cursor only while the mouse is the active device. Live gameplay always hides
        /// and captures it; pause and results release it again through <paramref name="gameplayActive"/>.
        /// </summary>
        public static (bool visible, CursorLockMode lockMode) Resolve(
            bool allowPointerInput,
            UiInputMode mode,
            bool gameplayActive,
            FullScreenMode fullScreenMode)
        {
            var fullscreen = fullScreenMode == FullScreenMode.FullScreenWindow
                || fullScreenMode == FullScreenMode.ExclusiveFullScreen;

            if (gameplayActive)
            {
                return (false, fullscreen ? CursorLockMode.Locked : CursorLockMode.Confined);
            }

            if (!allowPointerInput)
            {
                return (false, fullscreen ? CursorLockMode.Locked : CursorLockMode.None);
            }

            return (mode == UiInputMode.Mouse, CursorLockMode.None);
        }
    }
}
