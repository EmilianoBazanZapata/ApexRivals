using System.Collections;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class SceneLoadingOverlayPlayModeTests
    {
        private readonly PlayModeSceneIsolation _isolation = new PlayModeSceneIsolation();
        [SetUp] public void SetUp() => _isolation.Capture();
        [TearDown] public void TearDown() { Time.timeScale = 1f; _isolation.Cleanup(); }

        [UnityTest]
        public IEnumerator PausedOverlayAnimatesBlocksInputAndRecoversFocusOnFailure()
        {
            var root = new GameObject("OverlayTestBootstrap");
            var input = root.AddComponent<BootstrapUiInputSystem>();
            var overlay = root.AddComponent<SceneLoadingOverlay>();
            overlay.PreLoadDelaySeconds = 0f;
            overlay.Bind(input);
            var button = new GameObject("PreviousScreen", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            yield return null;
            UiFocus.Request(button);
            Time.timeScale = 0f;
            var shown = overlay.ShowAsync();
            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(input.IsLoadingInputBlocked, Is.True);
            Assert.That(root.GetComponent<InputSystemUIInputModule>().enabled, Is.False);
            Assert.That(UiFocus.CurrentSelection, Is.Null);
            var spinner = root.transform.Find("SceneLoadingOverlay/LoadingSpinner");
            var rotation = spinner.localRotation;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(shown.IsCompletedSuccessfully, Is.True);
            Assert.That(Quaternion.Angle(rotation, spinner.localRotation), Is.GreaterThan(0f));
            Assert.That(root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentInChildren<Canvas>().sortingOrder, Is.EqualTo(short.MaxValue));
            overlay.Hide(false);
            yield return null;
            yield return null;
            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(input.IsLoadingInputBlocked, Is.False);
            Assert.That(UiFocus.CurrentSelection, Is.EqualTo(button.gameObject));
            Assert.That(root.GetComponent<InputSystemUIInputModule>().enabled, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SuccessKeepsDestinationFocusAndDisableReleasesOnlyOwnedBlock()
        {
            var root = new GameObject("OverlayTestBootstrap");
            var input = root.AddComponent<BootstrapUiInputSystem>();
            var overlay = root.AddComponent<SceneLoadingOverlay>();
            overlay.PreLoadDelaySeconds = 0f;
            overlay.Bind(input);
            var showing = overlay.ShowAsync();
            while (!showing.IsCompleted) yield return null;
            var destination = new GameObject("Destination", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            UiFocus.Request(destination);
            overlay.Hide(true);
            Assert.That(UiFocus.CurrentSelection, Is.EqualTo(destination.gameObject));
            var otherOwner = new object();
            input.SetLoadingInputBlocked(otherOwner, true);
            overlay.PreLoadDelaySeconds = 5f;
            showing = overlay.ShowAsync();
            overlay.enabled = false;
            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(input.IsLoadingInputBlocked, Is.True);
            input.SetLoadingInputBlocked(otherOwner, false);
            Assert.That(input.IsLoadingInputBlocked, Is.False);
            while (!showing.IsCompleted) yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultFiveSecondDelayUsesRealTimeWhilePaused()
        {
            var root = new GameObject("DelayedOverlayTestBootstrap");
            var input = root.AddComponent<BootstrapUiInputSystem>();
            var overlay = root.AddComponent<SceneLoadingOverlay>();
            overlay.Bind(input);
            Assert.That(overlay.PreLoadDelaySeconds, Is.EqualTo(5f));
            Time.timeScale = 0f;
            var startedAt = Time.realtimeSinceStartupAsDouble;
            var showing = overlay.ShowAsync();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(showing.IsCompleted, Is.False);
            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(input.IsLoadingInputBlocked, Is.True);
            while (!showing.IsCompleted && Time.realtimeSinceStartupAsDouble - startedAt < 8d)
                yield return null;
            Assert.That(showing.IsCompletedSuccessfully, Is.True);
            Assert.That(Time.realtimeSinceStartupAsDouble - startedAt, Is.GreaterThanOrEqualTo(5d));
            Assert.That(Time.timeScale, Is.Zero);
            overlay.Hide(true);
            Assert.That(input.IsLoadingInputBlocked, Is.False);
        }
    }
}
