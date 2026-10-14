using System.Collections.Generic;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class UiCursorPolicyTests
    {
        [TestCase(FullScreenMode.FullScreenWindow, CursorLockMode.Locked)]
        [TestCase(FullScreenMode.ExclusiveFullScreen, CursorLockMode.Locked)]
        [TestCase(FullScreenMode.Windowed, CursorLockMode.None)]
        public void PointerDisabled_HidesCursorAndUsesFullscreenLock(FullScreenMode mode, CursorLockMode expectedLock)
        {
            var policy = UiCursorPolicy.Resolve(false, UiInputMode.Mouse, false, mode);

            Assert.That(policy.visible, Is.False);
            Assert.That(policy.lockMode, Is.EqualTo(expectedLock));
        }

        [TestCase(FullScreenMode.FullScreenWindow)]
        [TestCase(FullScreenMode.ExclusiveFullScreen)]
        [TestCase(FullScreenMode.Windowed)]
        public void MenusInMouseMode_ShowAndUnlockCursor(FullScreenMode mode)
        {
            var policy = UiCursorPolicy.Resolve(true, UiInputMode.Mouse, false, mode);

            Assert.That(policy.visible, Is.True);
            Assert.That(policy.lockMode, Is.EqualTo(CursorLockMode.None));
        }

        [TestCase(FullScreenMode.FullScreenWindow)]
        [TestCase(FullScreenMode.Windowed)]
        public void MenusInNavigationMode_HideCursorWithoutLocking(FullScreenMode mode)
        {
            var policy = UiCursorPolicy.Resolve(true, UiInputMode.Navigation, false, mode);

            Assert.That(policy.visible, Is.False);
            Assert.That(policy.lockMode, Is.EqualTo(CursorLockMode.None));
        }

        [TestCase(UiInputMode.Mouse, FullScreenMode.FullScreenWindow, CursorLockMode.Locked)]
        [TestCase(UiInputMode.Navigation, FullScreenMode.ExclusiveFullScreen, CursorLockMode.Locked)]
        [TestCase(UiInputMode.Mouse, FullScreenMode.Windowed, CursorLockMode.Confined)]
        public void LiveGameplay_HidesAndCapturesCursorInAnyMode(UiInputMode inputMode, FullScreenMode mode, CursorLockMode expectedLock)
        {
            var policy = UiCursorPolicy.Resolve(true, inputMode, true, mode);

            Assert.That(policy.visible, Is.False);
            Assert.That(policy.lockMode, Is.EqualTo(expectedLock));
        }

        [Test]
        public void MainMenuGarageAndRaceButtonsHaveVisualStateAndNoTintTransition()
        {
            var paths = new[]
            {
                "Assets/ApexRivals/Scenes/MainMenu.unity",
                "Assets/ApexRivals/Scenes/Garage.unity",
                "Assets/ApexRivals/Scenes/Race.unity"
            };
            var openedScenes = new List<Scene>();

            try
            {
                for (var sceneIndex = 0; sceneIndex < paths.Length; sceneIndex++)
                {
                    var scene = EditorSceneManager.OpenScene(paths[sceneIndex], OpenSceneMode.Additive);
                    openedScenes.Add(scene);
                    var roots = scene.GetRootGameObjects();
                    for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        var buttons = roots[rootIndex].GetComponentsInChildren<Button>(true);
                        for (var buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
                        {
                            var button = buttons[buttonIndex];
                            var hierarchyPath = GetHierarchyPath(button.transform);
                            Assert.That(
                                button.GetComponent<UguiMenuButtonVisualState>(),
                                Is.Not.Null,
                                $"Missing UguiMenuButtonVisualState: {paths[sceneIndex]} :: {hierarchyPath}");
                            Assert.That(
                                button.transition,
                                Is.EqualTo(Selectable.Transition.None),
                                $"Button must use Transition.None: {paths[sceneIndex]} :: {hierarchyPath}");

                            if (paths[sceneIndex].EndsWith("/Race.unity")
                                && (button.name == "ContinueButton"
                                    || button.name == "RestartRaceButton"
                                    || button.name == "ExitToMainMenuButton"))
                            {
                                Assert.That(button.targetGraphic, Is.Not.Null, hierarchyPath);
                                Assert.That(
                                    (Color32)button.targetGraphic.color,
                                    Is.EqualTo((Color32)ApexUiVisualStyle.Panel),
                                    $"Pause button should use the shared normal color: {hierarchyPath}");
                                Assert.That(
                                    button.GetComponent<Outline>(),
                                    Is.Null,
                                    $"Pause button should not draw the extra outline used by the old style: {hierarchyPath}");
                            }
                        }
                    }
                }
            }
            finally
            {
                for (var sceneIndex = openedScenes.Count - 1; sceneIndex >= 0; sceneIndex--)
                {
                    if (openedScenes[sceneIndex].IsValid() && openedScenes[sceneIndex].isLoaded)
                    {
                        EditorSceneManager.CloseScene(openedScenes[sceneIndex], true);
                    }
                }
            }
        }

        private static string GetHierarchyPath(Transform target)
        {
            var path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = $"{target.name}/{path}";
            }

            return path;
        }
    }
}
