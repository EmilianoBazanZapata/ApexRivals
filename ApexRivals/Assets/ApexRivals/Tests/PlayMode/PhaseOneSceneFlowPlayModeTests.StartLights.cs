#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator StartLights_AllCircuitsAnimateAndSoundOncePerAuthoritativeStage()
        {
            _installAllContentScenes = true;
            foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
            {
                _bootstrapper.Context.SelectedVehicleState.Commit(track == "RaceDunes" ? "striker" : "vanguard");
                _flow.SelectRaceScene(track);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                var lights = FindInScene<RaceStartLights>(track);
                var race = FindInScene<RaceCoordinator>(track);
                var observed = new bool[4];
                var deadline = Time.realtimeSinceStartup + 8;
                while (lights.Stage != -1 && Time.realtimeSinceStartup < deadline)
                {
                    var stage = lights.Stage;
                    Assert.That(stage, Is.InRange(1, 3));
                    observed[stage] = true;
                    Assert.That(lights.Label.text, Is.EqualTo(stage.ToString()));
                    Assert.That(lights.CueCount, Is.EqualTo(4 - stage), "Per-frame updates must not replay a cue.");
                    Assert.That(lights.IsVisible, Is.True);
                    Assert.That(race.ElapsedRaceTime, Is.Zero);
                    foreach (var racer in race.Participants)
                        Assert.That(racer.GetComponentInParent<DrivingInputGate>().DrivingAllowed, Is.False);
                    AssertLamps(lights, 4 - stage, false);
                    if (!observed[0])
                    {
                        Assert.That(lights.AudioSource.clip.name, Is.EqualTo("Countdown"));
                        AssertAudibleClip(lights.AudioSource.clip);
                        ScreenCapture.CaptureScreenshot("graphify-out/start-lights-" + track + "-red.png");
                        observed[0] = true;
                    }
                    yield return null;
                }
                Assert.That(observed, Is.All.True);
                Assert.That(lights.Stage, Is.EqualTo(-1));
                Assert.That(lights.CueCount, Is.EqualTo(4));
                Assert.That(lights.Label.text, Is.EqualTo("¡YA!"));
                Assert.That(lights.AudioSource.clip.name, Is.EqualTo("Go"));
                AssertAudibleClip(lights.AudioSource.clip);
                Assert.That(race.State, Is.EqualTo(RaceState.Racing));
                foreach (var racer in race.Participants)
                    Assert.That(racer.GetComponentInParent<DrivingInputGate>().DrivingAllowed, Is.True);
                AssertLamps(lights, 3, true);
                ScreenCapture.CaptureScreenshot("graphify-out/start-lights-" + track + "-green.png");
                yield return null;
                var goScale = lights.transform.Find("Panel").localScale.x;
                yield return new WaitForSeconds(0.3f);
                Assert.That(lights.transform.Find("Panel").localScale.x, Is.LessThan(goScale), "Animator must run the pulse.");
                yield return new WaitForSeconds(0.9f);
                Assert.That(lights.IsVisible, Is.False, "GO fades and clears the racing view.");
                Assert.That(lights.Animator.enabled, Is.False);
                Assert.That(lights.CueCount, Is.EqualTo(4));
            }
        }

        [UnityTest]
        public IEnumerator StartLights_PauseFreezesThenResumesAndRetryResets()
        {
            yield return WaitForTask(_flow.RetryRace());
            var lights = FindInRace<RaceStartLights>();
            var race = FindInRace<RaceCoordinator>();
            var session = Installer.Session;
            Assert.That(session.Pause().Succeeded, Is.True);
            var remaining = race.CountdownRemaining;
            var stage = lights.Stage;
            var cues = lights.CueCount;
            var normalized = lights.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Assert.That(lights.IsVisible, Is.False);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(race.CountdownRemaining, Is.EqualTo(remaining));
            Assert.That(lights.Stage, Is.EqualTo(stage));
            Assert.That(lights.CueCount, Is.EqualTo(cues));
            Assert.That(lights.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(normalized).Within(0.001f));
            Assert.That(session.Resume().Succeeded, Is.True);
            Assert.That(lights.IsVisible, Is.True);
            yield return null;
            Assert.That(lights.CueCount, Is.EqualTo(cues));
            yield return WaitForTask(_flow.RetryRace());
            var replacement = FindInRace<RaceStartLights>();
            Assert.That(lights == null, Is.True);
            Assert.That(replacement.Stage, Is.EqualTo(3));
            Assert.That(replacement.CueCount, Is.EqualTo(1));
            replacement.Uninstall();
            Assert.That(replacement.IsVisible, Is.False);
            yield return new WaitForSeconds(3.3f);
            Assert.That(replacement.Stage, Is.Zero, "Uninstalled presentation must not receive the next RaceStarted.");
            Assert.That(replacement.CueCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StartLights_StayHiddenThroughIntroductionAndSkipStartsOneCountdown()
        {
            _playIntroduction = true;
            yield return WaitForTask(_flow.RetryRace());
            var lights = FindInRace<RaceStartLights>();
            var intro = Installer.Introduction;
            var deadline = Time.realtimeSinceStartup + 4f;
            while (intro.ShotIndex < 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(intro.IsPresenting, Is.True);
            yield return new WaitForSeconds(0.35f);
            Assert.That(lights.IsVisible, Is.False);
            Assert.That(lights.Stage, Is.Zero);
            Assert.That(lights.CueCount, Is.Zero);
            Assert.That(Installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
            intro.RequestSkip();
            intro.RequestSkip();
            yield return null;
            Assert.That(lights.Stage, Is.EqualTo(3));
            Assert.That(lights.CueCount, Is.EqualTo(1));
            Assert.That(lights.IsVisible, Is.True);
        }

        private static void AssertLamps(RaceStartLights lights, int count, bool green)
        {
            for (var i = 0; i < 3; i++)
            {
                var color = lights.transform.Find("Panel/Lamp" + i).GetComponent<OctagonalStartLightGraphic>().color;
                if (i >= count) Assert.That(Mathf.Max(color.r, color.g, color.b), Is.LessThan(0.3f));
                else if (green) { Assert.That(color.g, Is.GreaterThan(0.9f)); Assert.That(color.r, Is.LessThan(0.3f)); }
                else { Assert.That(color.r, Is.GreaterThan(0.9f)); Assert.That(color.g, Is.LessThan(0.3f)); }
            }
        }

        private static void AssertAudibleClip(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            var energy = 0f;
            foreach (var sample in samples) energy += sample * sample;
            Assert.That(energy / samples.Length, Is.GreaterThan(0.01f));
        }
    }
}
#endif
