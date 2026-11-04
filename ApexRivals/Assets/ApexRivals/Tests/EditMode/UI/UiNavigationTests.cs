using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class UiNavigationTests
    {
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

        [TestCase(true, true, true, true)]
        [TestCase(false, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(false, false, false, false)]
        public void FocusAndNavigationAgreeOnHierarchyEnabledAndInteractable(bool parentActive, bool enabled, bool interactable, bool expected)
        {
            var parent = _objects.Own(new GameObject("Parent"));
            var button = CreateButton("Button", parent.transform);
            button.enabled = enabled;
            button.interactable = interactable;
            parent.SetActive(parentActive);

            Assert.That(UiFocus.IsUsable(button), Is.EqualTo(expected));
            Assert.That(UiNavigation.IsNavigable(button), Is.EqualTo(expected));
            Assert.That(UiNavigation.First(null, button), Is.EqualTo(expected ? button : null));
            Assert.That(UiFocus.IsUsable(null), Is.False);
            Assert.That(UiNavigation.IsNavigable(null), Is.False);
        }

        [Test]
        public void FocusResolvesEnabledSelectorsInAuthoredSettingsRows()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/ApexRivals/Scenes/MainMenu.unity");
            try
            {
                var selectorCount = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var selector in root.GetComponentsInChildren<SettingsOptionSelector>(true))
                    {
                        selectorCount++;
                        Assert.That(selector.enabled, Is.True);
                        Assert.That(UiFocus.SelectableOf(selector.gameObject), Is.SameAs(selector));
                    }
                }
                Assert.That(selectorCount, Is.GreaterThan(0));
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MainMenuPreparesLinksUnderHiddenParentsAndPreservesWrapping(bool wrapAround)
        {
            var root = _objects.Own(new GameObject("Menu"));
            var play = CreateButton("Play", root.transform);
            var garage = CreateButton("Garage", root.transform);
            var exit = CreateButton("Exit", root.transform);
            var navigation = play.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = wrapAround;
            play.navigation = navigation;
            garage.enabled = false;
            var controller = root.AddComponent<UguiMainMenuNavigationController>();
            using (var serialized = new SerializedObject(controller))
            {
                serialized.FindProperty("playButton").objectReferenceValue = play;
                serialized.FindProperty("garageButton").objectReferenceValue = garage;
                serialized.FindProperty("exitButton").objectReferenceValue = exit;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            root.SetActive(false);

            controller.RefreshNavigation();

            Assert.That(play.navigation.selectOnDown, Is.SameAs(garage));
            Assert.That(play.navigation.selectOnUp, Is.SameAs(exit));
            Assert.That(play.navigation.wrapAround, Is.EqualTo(wrapAround));
            Assert.That(UiFocus.IsUsable(garage), Is.False);
        }

        private Button CreateButton(string name, Transform parent)
        {
            var root = _objects.Own(new GameObject(name));
            root.transform.SetParent(parent);
            return root.AddComponent<Button>();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SharedWriterKeepsExplicitWrappingAndAllDirectionalLinks(bool wrapAround)
        {
            var root = _objects.Own(new GameObject("Links"));
            var button = CreateButton("Button", root.transform);
            var other = CreateButton("Other", root.transform);

            UiNavigation.Set(button, other, other, other, other, wrapAround);

            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(button.navigation.wrapAround, Is.EqualTo(wrapAround));
            Assert.That(button.navigation.selectOnUp, Is.SameAs(other));
            Assert.That(button.navigation.selectOnDown, Is.SameAs(other));
            Assert.That(button.navigation.selectOnLeft, Is.SameAs(other));
            Assert.That(button.navigation.selectOnRight, Is.SameAs(other));
        }
    }
}
