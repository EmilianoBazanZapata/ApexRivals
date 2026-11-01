#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator CarFades_ChangesShotsAtBlackAndConfirmationDuringFadeCleansUp()
        {
            _playIntroduction = _confirmCarPresentation = true;
            yield return WaitForTask(_flow.RetryRace());
            var intro = Installer.Introduction;
            var refs = new SerializedObject(intro);
            var fade = refs.FindProperty("shotFade").objectReferenceValue as CanvasGroup;
            var button = refs.FindProperty("startRaceButton").objectReferenceValue as Button;
            Assert.That(fade, Is.Not.Null);
            Assert.That(fade.blocksRaycasts, Is.False);
            var lastShot = -1;
            var cuts = 0;
            var sawOpaque = false;
            var sawPartial = false;
            var deadline = Time.realtimeSinceStartup + 10f;
            while (cuts < 3 && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
                if (intro.ShotIndex != lastShot && intro.ShotIndex >= 0)
                {
                    if (lastShot >= 0)
                    {
                        Assert.That(sawOpaque, Is.True, "Each camera change must be preceded by a fully opaque frame.");
                        cuts++;
                    }
                    lastShot = intro.ShotIndex;
                    sawOpaque = false;
                }
                if (fade.alpha >= .999f) sawOpaque = true;
                if (fade.alpha > .05f && fade.alpha < .95f) sawPartial = true;
                yield return null;
            }
            Assert.That(cuts, Is.GreaterThanOrEqualTo(3), "Must include the last-to-first loop transition.");
            Assert.That(sawPartial, Is.True);
            deadline = Time.realtimeSinceStartup + 3f;
            while ((fade.alpha <= .1f || fade.alpha >= .9f) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(fade.alpha, Is.InRange(.1f, .9f));
            Assert.That(button.interactable, Is.True, "Controls must remain usable during a fade.");
            button.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 3f;
            while (intro.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(intro.IsPresenting, Is.False);
            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
            Assert.That(fade.alpha, Is.Zero);
            Assert.That(fade.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator CarFades_CancelClearsOverlayAndDisabledOrZeroDurationModesStillStart()
        {
            _playIntroduction = _confirmCarPresentation = true;
            yield return WaitForTask(_flow.RetryRace());
            var intro = Installer.Introduction;
            var refs = new SerializedObject(intro);
            var fade = refs.FindProperty("shotFade").objectReferenceValue as CanvasGroup;
            var deadline = Time.realtimeSinceStartup + 3f;
            while ((intro.ShotIndex < 0 || fade.alpha <= .05f || fade.alpha >= .95f) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(fade.alpha, Is.InRange(.05f, .95f));
            intro.Cancel();
            Assert.That(fade.alpha, Is.Zero);
            Assert.That(fade.gameObject.activeSelf, Is.False);
            refs.Update();
            refs.FindProperty("fadeBetweenShots").boolValue = false;
            refs.ApplyModifiedPropertiesWithoutUndo();
            intro.Begin(_bootstrapper.Context, Installer.Session, FindInRace<RaceCoordinator>());
            deadline = Time.realtimeSinceStartup + 4f;
            while (intro.CompletedPresentationLoops < 1 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(intro.CompletedPresentationLoops, Is.GreaterThanOrEqualTo(1));
            Assert.That(fade.alpha, Is.Zero);
            Assert.That(fade.gameObject.activeSelf, Is.False);
            intro.Cancel();
            refs.Update();
            refs.FindProperty("fadeBetweenShots").boolValue = true;
            refs.FindProperty("fadeOutSeconds").floatValue = 0f;
            refs.FindProperty("fadeInSeconds").floatValue = 0f;
            refs.ApplyModifiedPropertiesWithoutUndo();
            intro.Begin(_bootstrapper.Context, Installer.Session, FindInRace<RaceCoordinator>());
            deadline = Time.realtimeSinceStartup + 4f;
            while (intro.CompletedPresentationLoops < 1 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(intro.CompletedPresentationLoops, Is.GreaterThanOrEqualTo(1));
            Assert.That(fade.alpha, Is.Zero);
            intro.RequestSkip();
            deadline = Time.realtimeSinceStartup + 3f;
            while (intro.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(intro.IsPresenting, Is.False);
            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
            Assert.That(fade.alpha, Is.Zero);
            Assert.That(fade.gameObject.activeSelf, Is.False);
        }
    }
}
#endif
