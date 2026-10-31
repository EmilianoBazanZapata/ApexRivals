using System;
using System.Linq;
using System.Reflection;
using ApexRivals.Audio.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VFX.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ApexRivals.Tests.EditMode
{
    public sealed class RemainingInspectorPreservationTests
    {
        private GameObject _root;
        private SceneSetup[] _setup;

        [SetUp]
        public void SetUp()
        {
            _setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _root = new GameObject("RemainingInspectorTest");
            _root.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            if (_setup.Any(scene => scene.isLoaded && !string.IsNullOrEmpty(scene.path))) EditorSceneManager.RestoreSceneManagerSetup(_setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SkidPitchUsesAuthoredBaseOrLeavesSourceUntouched(bool modulate)
        {
            var controller = _root.AddComponent<WheelArcadeVehicleController>();
            var source = _root.AddComponent<AudioSource>();
            source.pitch = 1.7f;
            var bus = _root.AddComponent<AudioBusSource>();
            var effects = _root.AddComponent<VehicleEffectsAudio>();
            Set(effects, "skid", bus);
            Set(effects, "_controller", controller);
            Set(effects, "modulatePitch", modulate);
            Set(effects, "skidPitchMultiplier", new Vector2(0.5f, 0.5f));
            Call(effects, "CaptureSourcePitches");
            Call(effects, "Update");
            Assert.That(source.pitch, Is.EqualTo(modulate ? 0.85f : 1.7f).Within(0.0001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelectorPreservesTransitionAndSupportsNativeOrPaletteVisuals(bool palette)
        {
            var go = new GameObject("Selector", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root.transform);
            var image = go.GetComponent<Image>();
            image.color = new Color(.2f, .3f, .4f);
            var selector = go.AddComponent<SettingsOptionSelector>();
            selector.targetGraphic = image;
            selector.transition = Selectable.Transition.ColorTint;
            Set(selector, "usePaletteVisuals", palette);
            Call(selector, "Awake");
            selector.SetOptions(new[] { "A", "B" }, 0);
            Assert.That(selector.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            if (!palette) Assert.That(image.color, Is.EqualTo(new Color(.2f, .3f, .4f)));
            else Assert.That(image.color, Is.EqualTo(ApexUiPalette.Resolve(null).PanelElevated));
        }

        [TestCase(ParticleSystemCurveMode.Constant)]
        [TestCase(ParticleSystemCurveMode.TwoConstants)]
        [TestCase(ParticleSystemCurveMode.Curve)]
        [TestCase(ParticleSystemCurveMode.TwoCurves)]
        public void WindOffsetsCurvesWithoutChangingTheirShapeOrSource(ParticleSystemCurveMode mode)
        {
            var original = new ParticleSystem.MinMaxCurve(2f,
                AnimationCurve.Linear(0, 1, 1, 3), AnimationCurve.Linear(0, 4, 1, 8));
            original.constantMin = 3f;
            original.constantMax = 7f;
            original.mode = mode;
            var method = typeof(RaceVfxSceneInstaller).GetMethod("OffsetCurve", BindingFlags.NonPublic | BindingFlags.Static);
            var shifted = (ParticleSystem.MinMaxCurve)method.Invoke(null, new object[] { original, -4f });
            Assert.That(shifted.mode, Is.EqualTo(mode));
            foreach (var t in new[] { 0f, .25f, .7f, 1f })
                foreach (var factor in new[] { 0f, .5f, 1f })
                    Assert.That(shifted.Evaluate(t, factor), Is.EqualTo(original.Evaluate(t, factor) - 4f).Within(.0001f));
            Assert.That(original.curveMax.Evaluate(1), Is.EqualTo(8f));
        }

        [TestCase(ParticleSystemSimulationSpace.Local)]
        [TestCase(ParticleSystemSimulationSpace.World)]
        public void RainWindIsAdditiveDoesNotAccumulateAndManualModeRestoresPrefab(ParticleSystemSimulationSpace space)
        {
            var rain = _root.AddComponent<ParticleSystem>();
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = space;
            velocity.x = new ParticleSystem.MinMaxCurve(2f, 5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-2f, -2f);
            velocity.z = new ParticleSystem.MinMaxCurve(3f, 3f);
            _root.transform.rotation = Quaternion.Euler(0, 90, 0);
            var installer = _root.AddComponent<RaceVfxSceneInstaller>();
            Set(installer, "_rain", rain);
            Set(installer, "_rainVelocityEnabled", true);
            Set(installer, "_rainX", velocity.x);
            Set(installer, "_rainY", velocity.y);
            Set(installer, "_rainZ", velocity.z);
            var worldWind = new Vector3(4f, 1f, 2f);
            var expected = space == ParticleSystemSimulationSpace.Local ? _root.transform.InverseTransformDirection(worldWind) : worldWind;
            Call(installer, "ApplyRainWind", worldWind);
            Call(installer, "ApplyRainWind", worldWind);
            Assert.That(velocity.x.constantMin, Is.EqualTo(2f + expected.x).Within(.0001f));
            Assert.That(velocity.x.constantMax, Is.EqualTo(5f + expected.x).Within(.0001f));
            Assert.That(velocity.y.constant, Is.EqualTo(-2f + expected.y).Within(.0001f));
            Assert.That(velocity.space, Is.EqualTo(space));
            Set(installer, "applyWeatherWind", false);
            Call(installer, "ApplyRainWind", worldWind);
            Assert.That(velocity.x.constantMin, Is.EqualTo(2f));
            Assert.That(velocity.x.constantMax, Is.EqualTo(5f));
            Assert.That(velocity.y.constant, Is.EqualTo(-2f));
            Assert.That(velocity.enabled, Is.True);
        }

        [Test]
        public void NitroBuilderKeepsCustomViewEvenWhenPanelHasAnotherName()
        {
            var hud = _root.AddComponent<RaceHudUguiView>();
            var custom = new GameObject("CustomBoostPanel");
            custom.transform.SetParent(_root.transform);
            var view = custom.AddComponent<NitroHudUguiView>();
            Set(hud, "nitroView", view);
            var result = EditorCall("NitroHudBuilder", "Create", hud);
            Assert.That(result, Is.SameAs(view));
            Assert.That(_root.transform.Find("NitroPanel"), Is.Null);
        }

        [Test]
        public void LoadingInstallerPreservesCustomSpinnerWhenSurfaceIsMissing()
        {
            _root.AddComponent<GameBootstrapper>();
            var loading = _root.GetComponent<SceneLoadingOverlay>();
            if (loading == null) loading = _root.AddComponent<SceneLoadingOverlay>();
            var custom = new GameObject("CustomSpinner", typeof(RectTransform));
            custom.transform.SetParent(_root.transform);
            Set(loading, "spinner", custom.transform);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ApexRivals/UI/Prefabs/SceneLoadingOverlay.prefab");
            Assert.That(prefab, Is.Not.Null);
            EditorCall("RuntimeUiPrefabsInstaller", "InstallLoadingOverlay", prefab);
            Assert.That(Get(loading, "spinner"), Is.SameAs(custom.transform));
            var surface = Get(loading, "surface");
            Assert.That(surface, Is.Not.Null);
            EditorCall("RuntimeUiPrefabsInstaller", "InstallLoadingOverlay", prefab);
            Assert.That(Get(loading, "surface"), Is.SameAs(surface));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IntroductionInstallerPreservesCustomShotsWithMissingRigOrSiblings(bool hasRig)
        {
            var presentation = _root.AddComponent<PreRacePresentation>();
            var cameraGo = new GameObject("Chase", typeof(UnityEngine.Camera), typeof(WheelVehicleFollowCamera));
            cameraGo.transform.SetParent(_root.transform);
            Set(presentation, "followCamera", cameraGo.GetComponent<WheelVehicleFollowCamera>());
            Set(presentation, "introductionHint", cameraGo);
            var rig = new GameObject("CustomRig");
            rig.transform.SetParent(_root.transform);
            var shotGo = new GameObject("AuthoredCamera", AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Cinemachine.CinemachineCamera")).First(t => t != null));
            shotGo.transform.SetParent(rig.transform);
            var custom = shotGo.GetComponent("CinemachineCamera");
            Set(presentation, "gridShot", custom);
            if (hasRig) Set(presentation, "shotRig", rig);
            EditorCall("RuntimeUiPrefabsInstaller", "InstallIntroduction", (object)null);
            Assert.That(Get(presentation, "gridShot"), Is.SameAs(custom));
            Assert.That(custom.transform.parent, Is.SameAs(rig.transform));
            if (hasRig)
            {
                Assert.That(Get(presentation, "rivalsShot"), Is.Not.Null);
                var count = rig.transform.childCount;
                EditorCall("RuntimeUiPrefabsInstaller", "InstallIntroduction", (object)null);
                Assert.That(rig.transform.childCount, Is.EqualTo(count));
            }
            else Assert.That(Get(presentation, "shotRig"), Is.Null, "A custom rig relationship must not be guessed.");
        }

        private static object EditorCall(string type, string method, params object[] args)
        {
            var found = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ApexRivals.Editor.UI." + type)).First(t => t != null);
            return found.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static object Get(object target, string name) => Field(target, name).GetValue(target);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
