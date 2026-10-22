using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Helpers for explicit keyboard/gamepad navigation graphs. Targets that are hidden, disabled or
    /// not interactable are skipped by passing fallbacks to <see cref="First"/>, so arrows never dead-end
    /// on a control the player cannot use.
    /// </summary>
    public static class UiNavigation
    {
        public static bool IsNavigable(Selectable selectable)
        {
            return selectable != null
                && selectable.enabled
                && selectable.gameObject.activeInHierarchy
                && selectable.IsInteractable();
        }

        /// <summary>First navigable candidate, in order; null when none is.</summary>
        public static Selectable First(params Selectable[] candidates)
        {
            for (var index = 0; index < candidates.Length; index++)
            {
                if (IsNavigable(candidates[index]))
                {
                    return candidates[index];
                }
            }

            return null;
        }

        public static void Set(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right, bool wrapAround = false)
        {
            if (selectable == null)
            {
                return;
            }

            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = wrapAround;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            selectable.navigation = navigation;
        }
    }
}
