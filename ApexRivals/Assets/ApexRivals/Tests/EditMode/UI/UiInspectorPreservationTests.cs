using System;
using System.Collections.Generic;
using System.Reflection;
using ApexRivals.Audio.Runtime;
using ApexRivals.Audio.Runtime.Bench;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class UiInspectorPreservationTests
    {
        private GameObject _root;
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("UiInspectorTest", typeof(RectTransform));
            _root.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void MainMenuRenderKeepsAuthoredStaticTextFontAndLayout()
        {
            var view = _root.AddComponent<MainMenuUguiView>();
            var fields = new[] { "startButtonPrimaryText", "startButtonDescriptionText", "garageButtonPrimaryText", "garageButtonDescriptionText", "settingsButtonPrimaryText", "settingsButtonDescriptionText", "quitButtonPrimaryText", "quitButtonDescriptionText" };
            var labels = new List<Text>();
            foreach (var field in fields)
            {
                var text = Child<Text>(field);
                text.text = "My " + field;
                text.fontSize = 37;
                text.color = Color.cyan;
                text.rectTransform.anchoredPosition = new Vector2(17, 29);
                Set(view, field, text);
                labels.Add(text);
            }
            view.Render(default);
            view.Render(default);
            foreach (var text in labels)
            {
                Assert.That(text.text, Is.EqualTo("My " + text.name));
                Assert.That(text.fontSize, Is.EqualTo(37));
                Assert.That(text.color, Is.EqualTo(Color.cyan));
                Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(17, 29)));
            }
        }

        [Test]
        public void RecoveryAwakeKeepsAuthoredMessage()
        {
            var view = _root.AddComponent<VehicleRecoveryPromptUguiView>();
            var text = Child<Text>("Prompt");
            text.text = "VOLVER A LA PISTA";
            Set(view, "promptText", text);
            Invoke(view, "Awake");
            view.Render(true);
            view.Render(false);
            Assert.That(text.text, Is.EqualTo("VOLVER A LA PISTA"));
        }

        [Test]
        public void ThumbnailRestoresAuthoredTintAcrossRepeatedLockChanges()
        {
            var view = _root.AddComponent<TrackSelectionCardView>();
            var image = Child<Image>("Preview");
            var authored = new Color(0.6f, 0.7f, 0.8f, 0.9f);
            image.color = authored;
            var palette = ScriptableObject.CreateInstance<ApexUiPalette>();
            _assets.Add(palette);
            Set(palette, "availableThumbnailTint", Color.white);
            Set(palette, "lockedThumbnailTint", new Color(0.5f, 0.4f, 0.3f, 1));
            Set(view, "palette", palette);
            Set(view, "thumbnail", image);
            view.Render(Track(true), false, true);
            Assert.That(image.color, Is.EqualTo(authored * palette.LockedThumbnailTint));
            view.Render(Track(false), false, true);
            view.Render(Track(false), false, true);
            Assert.That(image.color, Is.EqualTo(authored));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BadgeUsesAuthoredWidthOrConfiguredPadding(bool automatic)
        {
            var view = _root.AddComponent<TrackSelectionCardView>();
            var badge = Child<RectTransform>("Badge");
            badge.sizeDelta = new Vector2(321, 45);
            var label = Child<Text>("Label");
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Set(view, "badgeRoot", badge.gameObject);
            Set(view, "badgeText", label);
            Set(view, "autoSizeBadge", automatic);
            Set(view, "badgePadding", 93f);
            Set(view, "lockedLabel", "CERRADO");
            view.Render(Track(true), false, true);
            Assert.That(label.text, Is.EqualTo("CERRADO"));
            Assert.That(badge.sizeDelta.x, Is.EqualTo(automatic ? Mathf.Ceil(label.preferredWidth) + 93f : 321f));
            Assert.That(badge.sizeDelta.y, Is.EqualTo(45f));
        }

        [Test]
        public void ManualTachometerKeepsGaugeAndLabelConfiguration()
        {
            var view = _root.AddComponent<RaceHudUguiView>();
            var gauge = Child<ArcGaugeGraphic>("Gauge");
            gauge.MajorTicks = 5;
            gauge.RedZoneStart = 0.63f;
            var label = Child<Text>("Label");
            label.text = "CUSTOM";
            label.rectTransform.anchoredPosition = new Vector2(123, 456);
            Set(view, "tachometer", gauge);
            Set(view, "tachometerLabels", new[] { label });
            Set(view, "autoConfigureTachometer", false);
            Set(view, "autoSetTachometerLabels", false);
            Set(view, "autoLayoutTachometerLabels", false);
            view.BindTelemetry(new Telemetry());
            Assert.That(gauge.MajorTicks, Is.EqualTo(5));
            Assert.That(gauge.RedZoneStart, Is.EqualTo(0.63f));
            Assert.That(label.text, Is.EqualTo("CUSTOM"));
            Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(123, 456)));
        }

        [Test]
        public void AutomaticTachometerUsesInspectorStepRedZoneAndLabelOffset()
        {
            var view = _root.AddComponent<RaceHudUguiView>();
            var gauge = Child<ArcGaugeGraphic>("Gauge");
            gauge.rectTransform.sizeDelta = new Vector2(500, 500);
            var label = Child<Text>("Label");
            Set(view, "tachometer", gauge);
            Set(view, "tachometerLabels", new[] { label });
            Set(view, "tachometerTickStepRpm", 2000f);
            Set(view, "redZoneRedlineRatio", 0.7f);
            Set(view, "tachometerLabelRadiusOffset", -57f);
            Set(view, "tachometerLabelOffset", new Vector2(4, 9));
            view.BindTelemetry(new Telemetry());
            Assert.That(gauge.MajorTicks, Is.EqualTo(4));
            Assert.That(gauge.RedZoneStart, Is.EqualTo(7500f * 0.7f / 8000f).Within(0.00001f));
            Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(gauge.PointAt(0, -57f) + new Vector2(4, 9)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReferenceFitterUsesManualScaleOrConfiguredBaseScale(bool automatic)
        {
            var parent = (RectTransform)_root.transform;
            parent.sizeDelta = new Vector2(1920, 1080);
            var frame = Child<RectTransform>("Frame");
            frame.localScale = new Vector3(3, 4, 5);
            var fitter = frame.gameObject.AddComponent<UiReferenceFrameFitter>();
            Set(fitter, "automaticScaling", automatic);
            Set(fitter, "previewInEditMode", true);
            Set(fitter, "referenceSize", new Vector2(960, 540));
            Set(fitter, "baseScale", new Vector3(1.2f, 0.9f, 0.5f));
            frame.localScale = new Vector3(3, 4, 5);
            Invoke(fitter, "OnValidate");
            Invoke(fitter, "Fit");
            Assert.That(frame.localScale, Is.EqualTo(automatic ? new Vector3(2.4f, 1.8f, 0.5f) : new Vector3(3, 4, 5)));
        }

        [TestCase(0f, 1f)]
        [TestCase(0f, 100f)]
        [TestCase(-20f, 80f)]
        public void AudioSliderKeepsAuthoredRangeAndDoesNotFireChangeEvents(float minimum, float maximum)
        {
            var slider = Child<Slider>("Volume");
            slider.minValue = minimum;
            slider.maxValue = maximum;
            var events = 0;
            slider.onValueChanged.AddListener(_ => events++);
            InvokeStatic(typeof(SettingsUguiView), "SetSlider", slider, 0.25f);
            Assert.That(slider.minValue, Is.EqualTo(minimum));
            Assert.That(slider.maxValue, Is.EqualTo(maximum));
            Assert.That(slider.value, Is.EqualTo(Mathf.Lerp(minimum, maximum, 0.25f)));
            Assert.That(events, Is.Zero);
            Assert.That((float)InvokeStatic(typeof(SettingsUguiView), "NormalizedSliderValue", slider, slider.value), Is.EqualTo(0.25f));
        }

        [Test]
        public void FitterDoesNotChangeAuthoredScaleInEditModeUnlessPreviewIsEnabled()
        {
            var frame = Child<RectTransform>("Frame");
            var fitter = frame.gameObject.AddComponent<UiReferenceFrameFitter>();
            frame.localScale = new Vector3(3, 4, 5);
            Invoke(fitter, "OnValidate");
            Invoke(fitter, "Fit");
            Assert.That(frame.localScale, Is.EqualTo(new Vector3(3, 4, 5)));
        }

        [Test]
        public void DynamicErrorUsesConfiguredText()
        {
            var view = _root.AddComponent<MainMenuUguiView>();
            Set(view, "openGarageError", "NO SE PUDO ABRIR");
            Assert.That(Invoke(view, "StatusText", "ui.main.openGarageFailed"), Is.EqualTo("NO SE PUDO ABRIR"));
        }

        [Test]
        public void CylinderLampsUseConfiguredStateColours()
        {
            var view = _root.AddComponent<EngineSoundBench>();
            var synth = _root.AddComponent<ProceduralEngineSynth>();
            var lamp = Child<Image>("Lamp");
            var profile = ScriptableObject.CreateInstance<EngineSoundProfile>();
            _assets.Add(profile);
            Set(view, "synth", synth);
            Set(view, "cylinderLamps", new[] { lamp });
            Set(view, "firingLampColor", Color.magenta);
            Set(view, "restingLampColor", Color.green);
            Invoke(view, "RenderUi", profile);
            Assert.That(lamp.color, Is.EqualTo(Color.magenta));
            Set(view, "_running", false);
            Invoke(view, "RenderUi", profile);
            Assert.That(lamp.color, Is.EqualTo(Color.green));
        }

        [TestCase("garage", false)]
        [TestCase("garage", true)]
        [TestCase("trackCard", false)]
        [TestCase("trackCard", true)]
        [TestCase("trackPreview", false)]
        [TestCase("trackPreview", true)]
        public void PreviewsUseAutomaticAspectOrPreserveManualAspectAcrossSpriteChanges(string kind, bool automatic)
        {
            var image = Child<Image>("Preview");
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1.37f;
            MonoBehaviour view;
            if (kind == "garage")
            {
                view = _root.AddComponent<GarageUguiView>();
                Set(view, "vehicleTileFrames", new[] { Child<AngledPanelGraphic>("Frame") });
                Set(view, "vehicleTileThumbnails", new[] { image });
            }
            else if (kind == "trackCard")
            {
                view = _root.AddComponent<TrackSelectionCardView>();
                Set(view, "thumbnail", image);
            }
            else
            {
                view = _root.AddComponent<TrackSelectionUguiView>();
                Set(view, "previewImage", image);
            }
            // The automatic branch deliberately uses the default on a newly loaded component.
            if (!automatic) Set(view, "autoFitPreviewAspect", false);
            var texture = new Texture2D(12, 12);
            _assets.Add(texture);
            var wide = Sprite.Create(texture, new Rect(0, 0, 12, 6), Vector2.one * .5f);
            var tall = Sprite.Create(texture, new Rect(0, 0, 6, 12), Vector2.one * .5f);
            _assets.Add(wide);
            _assets.Add(tall);
            foreach (var sprite in new[] { wide, tall, wide })
            {
                RenderAspectPreview(view, image, sprite);
                Assert.That(image.sprite, Is.SameAs(sprite), "Preview content must still update in either mode.");
                Assert.That(fitter.aspectRatio, Is.EqualTo(automatic ? sprite.rect.width / sprite.rect.height : 1.37f).Within(.0001f));
                Assert.That(fitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
                Assert.That(fitter.enabled, Is.True);
            }
            Set(view, "autoFitPreviewAspect", false);
            fitter.aspectRatio = 1.23f;
            RenderAspectPreview(view, image, tall);
            RenderAspectPreview(view, image, null);
            Assert.That(fitter.aspectRatio, Is.EqualTo(1.23f));
            Assert.That(image.enabled, Is.False);
            Set(view, "autoFitPreviewAspect", true);
            RenderAspectPreview(view, image, wide);
            Assert.That(image.enabled, Is.True);
            Assert.That(fitter.aspectRatio, Is.EqualTo(2f));
        }

        private static void RenderAspectPreview(MonoBehaviour view, Image image, Sprite sprite)
        {
            if (view is GarageUguiView garage)
            {
                var vehicle = new VehicleSelectionItemViewModel("car", "Car", default, default, true, true, sprite);
                var model = new GarageViewModel(0, "Car", new[] { vehicle }, 0, true, default, default, default, false, default, string.Empty);
                Invoke(garage, "RenderVehicleStrip", model, 0, 1);
            }
            else if (view is TrackSelectionCardView card)
                card.Render(new TrackSelectionItemViewModel("track", "Track", "", "", 3, "", "", sprite, null), false, true);
            else Invoke(view, "SetPreview", sprite);
        }


        [Test]
        public void AmbienceSliderRendersWithoutEventsAndPercentageFollowsSilentUpdates()
        {
            var view = _root.AddComponent<SettingsUguiView>();
            var slider = Child<Slider>("Ambience");
            slider.minValue = 10; slider.maxValue = 110;
            Set(view, "ambienceVolumeSlider", slider);
            var label = Child<Text>("Percentage");
            var valueLabel = slider.gameObject.AddComponent<SliderValueLabel>();
            Set(valueLabel, "slider", slider); Set(valueLabel, "label", label);
            var events = 0;
            slider.onValueChanged.AddListener(_ => events++);
            view.Render(new SettingsViewModel(Array.Empty<ApexRivals.Settings.Runtime.SettingsResolution>(), default,
                FullScreenMode.Windowed, 1, 1, 1, 1, false, false, default, "", .35f));
            Invoke(valueLabel, "LateUpdate");
            Assert.That(slider.value, Is.EqualTo(45f));
            Assert.That(label.text, Is.EqualTo("35%"));
            Assert.That(events, Is.Zero);
            slider.SetValueWithoutNotify(110);
            Invoke(valueLabel, "LateUpdate");
            Assert.That(label.text, Is.EqualTo("100%"));
            Assert.That(events, Is.Zero);
        }


        private T Child<T>(string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(_root.transform, false);
            return typeof(T) == typeof(RectTransform) ? child.GetComponent<T>() : child.AddComponent<T>();
        }
        private static TrackSelectionItemViewModel Track(bool locked) => new TrackSelectionItemViewModel("id", "Track", "", "", 3, "", "", null, null, locked);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static object InvokeStatic(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private sealed class Telemetry : IVehicleTelemetry, IVehicleTachometer
        {
            public float SpeedKph => 100;
            public int CurrentGear => 3;
            public float EngineRpm => 4000;
            public bool IsReversing => false;
            public float RedlineRpm => 7500;
        }
    }
}
