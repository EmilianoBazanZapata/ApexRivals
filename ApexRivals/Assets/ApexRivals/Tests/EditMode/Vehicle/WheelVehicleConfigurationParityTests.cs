using System;
using System.Reflection;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Vehicle
{
    public sealed class WheelVehicleConfigurationParityTests
    {
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown() => _objects.Dispose();

        [Test]
        public void ConversionPreservesEveryTuningFieldWithNonDefaultValues()
        {
            var configuration = _objects.Own(ScriptableObject.CreateInstance<WheelVehicleConfiguration>());
            var ordinal = 0;
            foreach (var field in typeof(WheelVehicleTuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var source = typeof(WheelVehicleConfiguration).GetField(field.Name);
                Assert.That(source, Is.Not.Null, field.Name);
                Assert.That(source.FieldType, Is.EqualTo(field.FieldType), field.Name);
                object value;
                if (field.FieldType == typeof(float)) value = ++ordinal + 0.375f;
                else if (field.FieldType == typeof(bool)) value = !(bool)source.GetValue(configuration);
                else if (field.FieldType == typeof(float[])) value = new[] { 0.8f, 1.3f, 3.2f };
                else if (field.FieldType == typeof(AnimationCurve)) value = AnimationCurve.Linear(0.1f, 0.2f, 0.8f, 0.7f);
                else if (field.FieldType == typeof(WheelFrictionSettings))
                    value = new WheelFrictionSettings { extremumSlip = 0.11f + ++ordinal, extremumValue = 1.2f,
                        asymptoteSlip = 0.22f, asymptoteValue = 0.8f, stiffness = 1.4f };
                else throw new NotSupportedException("Characterize the new tuning field: " + field.Name);
                source.SetValue(configuration, value);
            }

            AssertParity(configuration, configuration.CreateTuning());
        }

        [TestCase("Vanguard")]
        [TestCase("Striker")]
        public void ProductionAssetConversionPreservesAuthoredValues(string vehicle)
        {
            var configuration = AssetDatabase.LoadAssetAtPath<WheelVehicleConfiguration>(
                $"Assets/ApexRivals/Vehicle/Configuration/{vehicle}WheelVehicleConfiguration.asset");
            Assert.That(configuration, Is.Not.Null);
            AssertParity(configuration, configuration.CreateTuning());
        }

        [TestCase("Vanguard")]
        [TestCase("Striker")]
        public void ControllerOverridesAndRepeatedUpgradesPreserveTheAuthoredBaseline(string vehicle)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ApexRivals/Vehicle/Prefabs/Vehicle_{vehicle}.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = _objects.Own(UnityEngine.Object.Instantiate(prefab));
            var controller = instance.GetComponent<WheelArcadeVehicleController>();
            Assert.That(controller, Is.Not.Null);
            var configuration = Get<WheelVehicleConfiguration>(controller, "configuration");
            Assert.That(configuration, Is.Not.Null);
            var authored = configuration.CreateTuning();
            controller.CopyConfigurationToRuntimeTuningOverrides();
            AssertParity(configuration, Get<WheelVehicleTuning>(controller, "runtimeTuningOverrides"));
            typeof(WheelArcadeVehicleController).GetField("useRuntimeTuningOverrides", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(controller, true);
            var baseline = configuration.CreatePerformanceStats();
            var upgraded = new VehiclePerformanceStats(baseline.Acceleration * 1.2f, baseline.TopSpeed * 1.1f,
                baseline.Steering * 1.25f, baseline.Handling * 1.3f, baseline.DriftHandling * 1.4f);

            for (var iteration = 0; iteration < 10; iteration++)
            {
                controller.ApplyPerformanceStats(upgraded);
                var effective = Get<WheelVehicleTuning>(controller, "_effectiveTuning");
                Assert.That(effective.motorPower, Is.EqualTo(authored.motorPower * 1.2f).Within(0.001f));
                Assert.That(effective.frontSidewaysFriction.stiffness, Is.EqualTo(authored.frontSidewaysFriction.stiffness * 1.3f).Within(0.001f));
                Assert.That(effective.rearSidewaysFriction.stiffness, Is.EqualTo(authored.rearSidewaysFriction.stiffness * 1.3f).Within(0.001f));
                Assert.That(effective.driftRearSidewaysGripMultiplier, Is.EqualTo(authored.driftRearSidewaysGripMultiplier * 1.4f / 1.3f).Within(0.001f));
                Assert.That(Get<float>(controller, "_performanceSteeringMultiplier"), Is.EqualTo(1.25f).Within(0.001f));
                Assert.That(effective.mass, Is.EqualTo(authored.mass));
                Assert.That(effective.enginePowerCurve, Is.SameAs(authored.enginePowerCurve));
                Assert.That(effective.steeringCurve, Is.SameAs(authored.steeringCurve));
                Assert.That(effective.gearRatios, Is.SameAs(authored.gearRatios));
            }

            AssertParity(configuration, authored);
            AssertParity(configuration, Get<WheelVehicleTuning>(controller, "runtimeTuningOverrides"));
        }

        private static void AssertParity(WheelVehicleConfiguration configuration, WheelVehicleTuning tuning)
        {
            foreach (var field in typeof(WheelVehicleTuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var source = typeof(WheelVehicleConfiguration).GetField(field.Name);
                Assert.That(source, Is.Not.Null, field.Name);
                var expected = source.GetValue(configuration);
                var actual = field.GetValue(tuning);
                Assert.That(actual, Is.EqualTo(expected), field.Name);
                if (field.FieldType == typeof(float[]) || field.FieldType == typeof(AnimationCurve))
                    Assert.That(actual, Is.SameAs(expected), field.Name + " reference");
            }
        }

        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
