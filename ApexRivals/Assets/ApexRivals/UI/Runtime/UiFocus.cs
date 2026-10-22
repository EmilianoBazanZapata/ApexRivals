using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Single owner of EventSystem selection. Screens are handled by UguiScreenNavigator; modals push a
    /// scope so focus (keyboard, gamepad and hover) cannot leave them, and popping restores what was
    /// focused before. Nothing else in the UI should call EventSystem.SetSelectedGameObject.
    /// </summary>
    public static class UiFocus
    {
        private sealed class Scope
        {
            public GameObject Root;
            public Selectable Initial;
            public GameObject Restore;
        }

        private static readonly List<Scope> Scopes = new List<Scope>();

        public static bool HasActiveScope => ActiveScope() != null;

        public static GameObject ActiveScopeRoot => ActiveScope()?.Root;

        public static GameObject CurrentSelection => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        /// <summary>Focuses <paramref name="target"/> if it is usable and inside the active modal scope.</summary>
        public static bool Request(Selectable target)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.alreadySelecting || !IsUsable(target) || !IsInsideActiveScope(target.gameObject))
            {
                return false;
            }

            if (eventSystem.currentSelectedGameObject != target.gameObject)
            {
                eventSystem.SetSelectedGameObject(target.gameObject);
            }

            return true;
        }

        public static bool Request(GameObject target)
        {
            return Request(SelectableOf(target));
        }

        /// <summary>
        /// The enabled Selectable on <paramref name="target"/>. GetComponent alone is not enough: a row
        /// can keep a disabled legacy Selectable (e.g. an old Slider) next to the live one, and treating
        /// that as "the" selectable makes a valid selection look invalid.
        /// </summary>
        public static Selectable SelectableOf(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            var selectables = target.GetComponents<Selectable>();
            for (var index = 0; index < selectables.Length; index++)
            {
                if (selectables[index].enabled)
                {
                    return selectables[index];
                }
            }

            return selectables.Length > 0 ? selectables[0] : null;
        }

        public static void Clear()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && !eventSystem.alreadySelecting && eventSystem.currentSelectedGameObject != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        /// <summary>Opens a modal focus scope and focuses its preferred (or first usable) control.</summary>
        public static void PushScope(GameObject root, Selectable initial)
        {
            if (root == null)
            {
                return;
            }

            PopScope(root, restoreSelection: false);
            Scopes.Add(new Scope { Root = root, Initial = initial, Restore = CurrentSelection });
            Request(FirstUsable(initial, root));
        }

        /// <summary>Closes the scope opened for <paramref name="root"/> and restores the previous focus when still valid.</summary>
        public static void PopScope(GameObject root, bool restoreSelection = true)
        {
            for (var index = Scopes.Count - 1; index >= 0; index--)
            {
                if (Scopes[index].Root != root)
                {
                    continue;
                }

                var restore = Scopes[index].Restore;
                Scopes.RemoveAt(index);
                if (restoreSelection && !Request(restore))
                {
                    RepairActiveScope();
                }

                return;
            }
        }

        /// <summary>Keeps focus inside the active modal scope. Returns false when no scope is open.</summary>
        public static bool RepairActiveScope()
        {
            var scope = ActiveScope();
            if (scope == null)
            {
                return false;
            }

            var selected = CurrentSelection;
            if (selected != null && IsInside(selected, scope.Root) && IsUsable(SelectableOf(selected)))
            {
                return true;
            }

            Request(FirstUsable(scope.Initial, scope.Root));
            return true;
        }

        public static bool IsInsideActiveScope(GameObject target)
        {
            var scope = ActiveScope();
            return scope == null || (target != null && IsInside(target, scope.Root));
        }

        public static bool IsUsable(Selectable selectable)
        {
            return UiNavigation.IsNavigable(selectable);
        }

        public static Selectable FirstUsable(Selectable preferred, GameObject root)
        {
            if (IsUsable(preferred) && (root == null || IsInside(preferred.gameObject, root)))
            {
                return preferred;
            }

            if (root == null || !root.activeInHierarchy)
            {
                return null;
            }

            var selectables = root.GetComponentsInChildren<Selectable>(false);
            for (var index = 0; index < selectables.Length; index++)
            {
                if (IsUsable(selectables[index]))
                {
                    return selectables[index];
                }
            }

            return null;
        }

        /// <summary>Drops every scope. Called when the UI input owner boots and from tests.</summary>
        public static void Reset()
        {
            Scopes.Clear();
        }

        private static Scope ActiveScope()
        {
            // Scopes whose root was destroyed or hidden without an explicit pop (e.g. the whole pause
            // menu closed) must not keep trapping focus.
            for (var index = Scopes.Count - 1; index >= 0; index--)
            {
                var root = Scopes[index].Root;
                if (root == null || !root.activeInHierarchy)
                {
                    Scopes.RemoveAt(index);
                    continue;
                }

                return Scopes[index];
            }

            return null;
        }

        private static bool IsInside(GameObject target, GameObject root)
        {
            return target != null && root != null && target.transform.IsChildOf(root.transform);
        }
    }
}
