using System.Text;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Editor.UI
{
    /// <summary>
    /// Gives every race scene's pause menu a SETTINGS button that opens the same Settings screen as the main menu.
    /// The screen is copied from the main menu scene, so re-running after the main menu's Settings changes (e.g. a
    /// new tab) brings those changes to the races too. Re-running replaces the copy and the button.
    /// </summary>
    public static class PauseSettingsBuilder
    {
        private const string MainMenuScenePath = "Assets/ApexRivals/Scenes/MainMenu.unity";
        private const string ScreenName = "SettingsScreen";
        private const string ButtonName = "SettingsButton";

        private static readonly string[] RaceScenes =
        {
            "Assets/ApexRivals/Scenes/Race.unity",
            "Assets/ApexRivals/Scenes/RaceDunes.unity",
            "Assets/ApexRivals/Scenes/RaceSuzuka.unity"
        };

        [MenuItem("ApexRivals/UI/Rebuild Pause Settings")]
        public static void RebuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(RebuildAll());
            }
        }

        public static string RebuildAll()
        {
            var previous = EditorSceneManager.GetActiveScene().path;
            var report = new StringBuilder();
            foreach (var path in RaceScenes)
            {
                report.AppendLine(path + ": " + Rebuild(path));
            }

            if (!string.IsNullOrEmpty(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }

            return report.ToString();
        }

        private static string Rebuild(string racePath)
        {
            var race = EditorSceneManager.OpenScene(racePath, OpenSceneMode.Single);
            var menu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
            try
            {
                var sourceView = FindInScene<SettingsUguiView>(menu);
                var menuNavigator = FindInScene<UguiScreenNavigator>(menu);
                var installer = FindInScene<RaceUiSceneInstaller>(race);
                var navigator = FindInScene<UguiScreenNavigator>(race);
                var pauseView = FindInScene<PauseUguiView>(race);
                if (sourceView == null || installer == null || navigator == null || pauseView == null)
                {
                    return "missing Settings, race installer, navigator or pause view";
                }

                var canvas = pauseView.GetComponentInParent<Canvas>(true).transform;
                var existing = canvas.Find(ScreenName);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                // Copy the main menu's Settings screen; Instantiate under a race parent places it in the race scene.
                var screen = Object.Instantiate(sourceView.gameObject, canvas, false);
                screen.name = ScreenName;
                screen.SetActive(false);
                var navigatorRoot = new SerializedObject(navigator).FindProperty("pausePanel").objectReferenceValue as GameObject;
                if (navigatorRoot != null && navigatorRoot.transform.parent == canvas)
                {
                    screen.transform.SetSiblingIndex(navigatorRoot.transform.GetSiblingIndex() + 1);
                }

                var settingsButton = AddSettingsButton(pauseView);

                var pauseRefs = new SerializedObject(pauseView);
                pauseRefs.FindProperty("settingsButton").objectReferenceValue = settingsButton;
                pauseRefs.ApplyModifiedPropertiesWithoutUndo();

                var installerRefs = new SerializedObject(installer);
                installerRefs.FindProperty("settingsView").objectReferenceValue = screen.GetComponent<SettingsUguiView>();
                installerRefs.ApplyModifiedPropertiesWithoutUndo();

                var navigatorRefs = new SerializedObject(navigator);
                navigatorRefs.FindProperty("settingsPanel").objectReferenceValue = screen;
                navigatorRefs.FindProperty("settingsInitialSelection").objectReferenceValue = MatchInCopy(
                    new SerializedObject(menuNavigator).FindProperty("settingsInitialSelection").objectReferenceValue as Selectable,
                    sourceView.transform,
                    screen.transform);
                navigatorRefs.ApplyModifiedPropertiesWithoutUndo();

                var theme = canvas.GetComponent<ApexUiTheme>();
                if (theme != null)
                {
                    theme.ApplyStyles();
                }

                EditorSceneManager.MarkSceneDirty(race);
                EditorSceneManager.SaveScene(race);
                return "pause SETTINGS button and Settings screen rebuilt";
            }
            finally
            {
                EditorSceneManager.CloseScene(menu, true);
            }
        }

        /// <summary>SETTINGS right under CONTINUE, styled like the other pause buttons.</summary>
        private static Button AddSettingsButton(PauseUguiView pauseView)
        {
            var panel = pauseView.transform;
            var existing = panel.Find(ButtonName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var continueButton = new SerializedObject(pauseView).FindProperty("resumeButton").objectReferenceValue as Button;
            var copy = Object.Instantiate(continueButton.gameObject, continueButton.transform.parent, false);
            copy.name = ButtonName;
            copy.transform.SetSiblingIndex(continueButton.transform.GetSiblingIndex() + 1);
            copy.GetComponentInChildren<Text>(true).text = "SETTINGS";
            var visual = copy.GetComponent<UguiMenuButtonVisualState>();
            if (visual != null)
            {
                visual.PrimaryAction = false;
            }

            return copy.GetComponent<Button>();
        }

        /// <summary>The object at the same path under <paramref name="copyRoot"/> as <paramref name="original"/> under <paramref name="sourceRoot"/>.</summary>
        private static Selectable MatchInCopy(Selectable original, Transform sourceRoot, Transform copyRoot)
        {
            if (original == null || !original.transform.IsChildOf(sourceRoot))
            {
                return null;
            }

            var path = original.transform == sourceRoot ? string.Empty : AnimationUtility.CalculateTransformPath(original.transform, sourceRoot);
            var match = path.Length == 0 ? copyRoot : copyRoot.Find(path);
            return match != null ? match.GetComponent<Selectable>() : null;
        }

        private static T FindInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
