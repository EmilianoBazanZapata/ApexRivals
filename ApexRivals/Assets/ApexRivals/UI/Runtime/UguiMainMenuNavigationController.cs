using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class UguiMainMenuNavigationController : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button playButton;
        [SerializeField] private Button garageButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        private void OnEnable()
        {
            RefreshNavigation();
        }

        public void RefreshNavigation()
        {
            var buttons = new[] { playButton, garageButton, settingsButton, exitButton };
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                if (button == null)
                {
                    continue;
                }

                var previous = FindEnabled(buttons, index, -1);
                var next = FindEnabled(buttons, index, 1);
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = previous;
                navigation.selectOnDown = next;
                navigation.selectOnLeft = previous;
                navigation.selectOnRight = next;
                button.navigation = navigation;
            }
        }

        private static Button FindEnabled(Button[] buttons, int startIndex, int direction)
        {
            if (buttons == null || buttons.Length == 0)
            {
                return null;
            }

            for (var offset = 1; offset <= buttons.Length; offset++)
            {
                var index = (startIndex + direction * offset + buttons.Length) % buttons.Length;
                if (IsEnabled(buttons[index]))
                {
                    return buttons[index];
                }
            }

            return null;
        }

        private static bool IsEnabled(Button button)
        {
            return button != null && button.gameObject.activeSelf && button.IsInteractable();
        }
    }
}
