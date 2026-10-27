#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Environment.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.VFX.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Weather_AllTracksTransitionPauseClearAndUnloadWithoutLeaking()
        {
            _dynamicWeather = _installAllContentScenes = true;
            foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
            {
                _flow.SelectRaceScene(track);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                var weather = FindInScene<RaceWeatherController>(track);
                var vfx = FindInScene<RaceVfxSceneInstaller>(track);
                Assert.That(weather, Is.Not.Null);
                Assert.That(weather.IsInstalled, Is.True);
                var deadline = Time.realtimeSinceStartup + 8;
                var session = FindInScene<ApexRivals.Bootstrap.Runtime.RaceSessionSceneInstaller>(track).Session;
                while (session.State == RaceSessionLifecycleState.Starting && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
                weather.SetForecast(new RaceWeatherForecast(RaceWeatherPattern.SunnyToRain, 0, .4f, 42));
                Assert.That(vfx.RainEnabled, Is.False);
                Assert.That(weather.WindAudio.volume, Is.Zero);
                yield return new WaitForSeconds(.7f);
                Assert.That(weather.Current.Rain, Is.EqualTo(1));
                Assert.That(vfx.RainEnabled, Is.True);
                Assert.That(vfx.Rain, Is.Not.Null);
                Assert.That(vfx.Rain.emission.rateOverTime.constant, Is.GreaterThan(0));
                Assert.That(weather.WindAudio.volume, Is.GreaterThan(0));
                Assert.That(RenderSettings.fog, Is.True);
                Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(45));
                Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(300));
                Assert.That(RenderSettings.skybox.GetColor("_HorizonColor"), Is.EqualTo(RenderSettings.fogColor));
                var rainVelocity = vfx.Rain.velocityOverLifetime;
                Assert.That(Mathf.Abs(rainVelocity.x.constant) + Mathf.Abs(rainVelocity.z.constant), Is.GreaterThan(0));
                ScreenCapture.CaptureScreenshot("graphify-out/dynamic-weather-rain-" + track + ".png");
                Assert.That(session.Pause().Succeeded, Is.True);
                yield return null;
                var elapsed = weather.Elapsed;
                var rain = weather.Current.Rain;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(weather.Elapsed, Is.EqualTo(elapsed));
                Assert.That(weather.Current.Rain, Is.EqualTo(rain));
                Assert.That(weather.WindAudio.isPlaying, Is.False);
                Assert.That(weather.RainAudio.isPlaying, Is.False);
                Assert.That(session.Resume().Succeeded, Is.True);
                weather.SetForecast(new RaceWeatherForecast(RaceWeatherPattern.RainToSunny, 0, .4f, 42));
                yield return new WaitForSeconds(.7f);
                Assert.That(weather.Current.Rain, Is.Zero);
                Assert.That(weather.Current.Wind, Is.Zero);
                Assert.That(weather.WindAudio.volume, Is.Zero);
                Assert.That(weather.RainAudio.volume, Is.Zero);
                Assert.That(vfx.RainEnabled, Is.False);
                Assert.That(vfx.Rain.particleCount, Is.Zero);
                Assert.That(RenderSettings.fog, Is.False);
                ScreenCapture.CaptureScreenshot("graphify-out/dynamic-weather-clear-" + track + ".png");
            }
            var oldWeather = FindInScene<RaceWeatherController>("RaceSuzuka");
            oldWeather.SetForecast(new RaceWeatherForecast(RaceWeatherPattern.Rainy, 0, 1, 42));
            var oldSky = RenderSettings.skybox;
            // This fixture's minimal context omits the main-menu vehicle-selection services.
            _installAllContentScenes = false;
            yield return WaitForTask(_flow.ForceReturnToMainMenu());
            yield return null;
            Assert.That(oldWeather == null, Is.True);
            Assert.That(oldSky == null, Is.True, "The transient weather material must be released.");
            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(RenderSettings.skybox == null || RenderSettings.skybox.name != "RaceWeatherSky (Instance)", Is.True);
        }
    }
}
#endif
