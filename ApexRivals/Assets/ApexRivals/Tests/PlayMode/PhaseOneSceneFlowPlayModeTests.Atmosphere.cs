#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.Environment.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.Settings.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Atmosphere_AllCircuitsPopulateExistingStandsWithoutColliders()
        {
            _installAllContentScenes = true;
            foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
            {
                _bootstrapper.Context.SelectedVehicleState.Commit(track == "RaceDunes" ? "striker" : "vanguard");
                _flow.SelectRaceScene(track);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                var atmosphere = FindInScene<TrackAtmosphere>(track);
                yield return new WaitForSeconds(0.15f);
                Assert.That(atmosphere.IsInstalled, Is.True);
                Assert.That(atmosphere.SpectatorCount, Is.GreaterThan(300));
                Assert.That(atmosphere.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(atmosphere.GetComponentsInChildren<LODGroup>().Length, Is.GreaterThan(5));
                Assert.That(atmosphere.CrowdSources.Length, Is.InRange(2, 12));
                foreach (var source in atmosphere.CrowdSources)
                {
                    Assert.That(source.isPlaying, Is.True);
                    Assert.That(source.spatialBlend, Is.EqualTo(1));
                    Assert.That(source.loop, Is.True);
                    Assert.That(source.playOnAwake, Is.False);
                }
                Assert.That(atmosphere.WeatherSource.clip.name, Is.EqualTo(track == "RaceSuzuka" ? "Rain" : "Wind"));
                var renderer = atmosphere.transform.Find("CrowdSection_0/Spectators").GetComponent<MeshRenderer>();
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_EnvironmentTime"), Is.EqualTo(atmosphere.AnimationClock).Within(0.001f));
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                var deadline = Time.realtimeSinceStartup + 6;
                var race = FindInScene<RaceCoordinator>(track);
                while (race.State == RaceState.Countdown && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSeconds(1.2f); // Photograph after GO has faded out.
                var follow = FindInScene<WheelVehicleFollowCamera>(track);
                follow.enabled = false;
                var section = atmosphere.transform.Find("CrowdSection_0");
                follow.transform.position = section.TransformPoint(new Vector3(0, 5, -18));
                follow.transform.LookAt(section.TransformPoint(new Vector3(0, 4.3f, -1)));
                yield return new WaitForSeconds(0.2f);
                ScreenCapture.CaptureScreenshot("graphify-out/atmosphere-" + track + "-crowd.png");
                yield return null;
                follow.transform.position = section.TransformPoint(new Vector3(12, 7, -30));
                follow.transform.LookAt(section.TransformPoint(new Vector3(0, 6.5f, 1)));
                yield return new WaitForSeconds(0.2f);
                ScreenCapture.CaptureScreenshot("graphify-out/atmosphere-" + track + ".png");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Atmosphere_PauseFreezesMotionAndAudioAndMasterPreviewControlsVolume()
        {
            var atmosphere = FindInRace<TrackAtmosphere>();
            var original = _bootstrapper.Context;
            var settings = new SettingsService(new MemorySettingsStorage(), new MemoryDisplaySettings());
            atmosphere.Install(new ApplicationContext(_flow, original.ProgressionState, original.SelectedVehicleState,
                original.SaveService, settingsService: settings, pauseCoordinator: original.PauseCoordinator));
            yield return new WaitForSeconds(0.1f);
            Assert.That(Installer.Session.Pause().Succeeded, Is.True);
            yield return null;
            yield return null;
            var clock = atmosphere.AnimationClock;
            var sample = atmosphere.WeatherSource.timeSamples;
            Assert.That(atmosphere.WeatherSource.isPlaying, Is.False);
            foreach (var source in atmosphere.CrowdSources) Assert.That(source.isPlaying, Is.False);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(atmosphere.AnimationClock, Is.EqualTo(clock));
            Assert.That(atmosphere.WeatherSource.timeSamples, Is.EqualTo(sample));
            Assert.That(Installer.Session.Resume().Succeeded, Is.True);
            yield return new WaitForSeconds(0.1f);
            Assert.That(atmosphere.AnimationClock, Is.GreaterThan(clock));
            Assert.That(atmosphere.WeatherSource.isPlaying, Is.True);
            settings.EditMasterVolume(0);
            yield return null;
            Assert.That(atmosphere.WeatherSource.volume, Is.Zero);
            foreach (var source in atmosphere.CrowdSources) Assert.That(source.volume, Is.Zero);
            settings.EditMasterVolume(0.5f);
            yield return null;
            Assert.That(atmosphere.CrowdSources[0].volume, Is.EqualTo(0.14f * 0.25f).Within(0.001f));
            atmosphere.Uninstall();
            Assert.That(atmosphere.WeatherSource.isPlaying, Is.False);
            Assert.That(atmosphere.IsInstalled, Is.False);
        }

        [UnityTest]
        public IEnumerator Atmosphere_StartAndFinishTriggerCheeringAndRetryDoesNotDuplicateSources()
        {
            yield return WaitForTask(_flow.RetryRace());
            var atmosphere = FindInRace<TrackAtmosphere>();
            var sources = atmosphere.GetComponentsInChildren<AudioSource>().Length;
            var deadline = Time.realtimeSinceStartup + 6;
            while (Installer.Session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(atmosphere.Cheer, Is.GreaterThan(0.9f));
            CompleteStabilityRace();
            Assert.That(atmosphere.Cheer, Is.GreaterThanOrEqualTo(0.5f));
            var finishClock = atmosphere.AnimationClock;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(atmosphere.AnimationClock, Is.GreaterThan(finishClock), "The crowd celebrates while results freeze vehicle physics.");
            Assert.That(atmosphere.CrowdSources[0].isPlaying, Is.True);
            var retry = Installer.Session.Retry();
            yield return WaitForTask(retry);
            Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
            var replacement = FindInRace<TrackAtmosphere>();
            Assert.That(atmosphere == null, Is.True);
            Assert.That(replacement.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(sources));
            replacement.Uninstall();
            yield return new WaitForSeconds(3.3f);
            Assert.That(replacement.Cheer, Is.Zero, "Uninstalled atmosphere must not receive RaceStarted.");
            foreach (var source in replacement.CrowdSources) Assert.That(source.isPlaying, Is.False);
        }
    }
}
#endif
