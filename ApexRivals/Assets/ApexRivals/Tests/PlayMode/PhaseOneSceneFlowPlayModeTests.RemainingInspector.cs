#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using ApexRivals.DrivingAssistance.Runtime;
using ApexRivals.Environment.Runtime;
using ApexRivals.VFX.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator InspectorOverrides_DrivingLineKeepsAuthoredRendererAfterReinstall()
        {
            _installAllContentScenes = true;
            yield return WaitForTask(_flow.RetryRace());
            var line = FindInRace<DynamicDrivingLine>();
            line.Uninstall();
            var renderer = line.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            line.Install(_bootstrapper.Context);
            Assert.That(line.MarkerCount, Is.GreaterThan(0));
            Assert.That(line.Mesh, Is.Not.Null);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));
            Assert.That(renderer.receiveShadows, Is.True);
            Assert.That(renderer.lightProbeUsage, Is.EqualTo(LightProbeUsage.BlendProbes));
            Assert.That(renderer.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.Simple));
        }

        [UnityTest]
        public IEnumerator InspectorOverrides_RainKeepsPrefabCurveAndManualVelocityAcrossIntensityChanges()
        {
            _installAllContentScenes = true;
            yield return WaitForTask(_flow.RetryRace());
            var installer = FindInRace<RaceVfxSceneInstaller>();
            var original = (RaceVfxDefinition)InspectorField(installer, "definition").GetValue(installer);
            var definition = Object.Instantiate(original);
            var template = Object.Instantiate(original.rainPrefab);
            template.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = template.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(2f, AnimationCurve.Linear(0, 10, 1, 30));
            var authored = emission.rateOverTime;
            var velocity = template.velocityOverLifetime;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.enabled = false;
            velocity.x = 3f; velocity.y = 2f; velocity.z = 1f;
            definition.rainPrefab = template;
            try
            {
                installer.Uninstall();
                SetInspectorField(installer, "definition", definition);
                SetInspectorField(installer, "enableRain", true);
                SetInspectorField(installer, "applyWeatherWind", false);
                installer.Install(_bootstrapper.Context);
                foreach (var intensity in new[] { .25f, .75f, .25f })
                {
                    installer.SetWeather(intensity, new Vector3(9, 0, 3));
                    var rate = installer.Rain.emission.rateOverTime;
                    Assert.That(rate.mode, Is.EqualTo(ParticleSystemCurveMode.Curve));
                    foreach (var t in new[] { 0f, .5f, 1f })
                        Assert.That(rate.Evaluate(t), Is.EqualTo(authored.Evaluate(t) * intensity).Within(.001f));
                    var actual = installer.Rain.velocityOverLifetime;
                    Assert.That(actual.enabled, Is.False);
                    Assert.That(actual.space, Is.EqualTo(ParticleSystemSimulationSpace.Local));
                    Assert.That(actual.x.constant, Is.EqualTo(3f));
                    Assert.That(actual.y.constant, Is.EqualTo(2f));
                    Assert.That(installer.Rain.isPlaying, Is.True);
                }
                Assert.That(template.emission.rateOverTimeMultiplier, Is.EqualTo(2f));
            }
            finally
            {
                installer.Uninstall();
                SetInspectorField(installer, "definition", original);
                Object.Destroy(template.gameObject);
                Object.Destroy(definition);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InspectorOverrides_WeatherSupportsManualLightingAndConfiguredModes()
        {
            _installAllContentScenes = _dynamicWeather = true;
            yield return WaitForTask(_flow.RetryRace());
            var weather = FindInRace<RaceWeatherController>();
            Assert.That(weather.IsInstalled, Is.True);
            SetInspectorField(weather, "controlFog", false);
            SetInspectorField(weather, "controlAmbient", false);
            weather.SetForecast(new RaceWeatherForecast(RaceWeatherPattern.Sunny, 0, 1, 42));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .123f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = Color.magenta;
            weather.SetForecast(new RaceWeatherForecast(RaceWeatherPattern.Rainy, 0, 1, 42));
            yield return null;
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.ExponentialSquared));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.123f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(Color.magenta));
            SetInspectorField(weather, "controlFog", true);
            SetInspectorField(weather, "controlAmbient", true);
            SetInspectorField(weather, "weatherFogMode", FogMode.Exponential);
            SetInspectorField(weather, "stormFogDensity", .07f);
            SetInspectorField(weather, "weatherAmbientMode", AmbientMode.Flat);
            weather.SetForecast(new RaceWeatherForecast(RaceWeatherPattern.Rainy, 0, 1, 42));
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Exponential));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.07f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
        }

        private static FieldInfo InspectorField(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void SetInspectorField(object target, string name, object value) => InspectorField(target, name).SetValue(target, value);
    }
}
#endif
