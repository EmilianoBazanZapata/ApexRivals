using ApexRivals.VFX.Runtime;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ApexRivals.Tests.EditMode.VFX
{
    public sealed class SkidmarkRulesTests
    {
        private RaceVfxDefinition _configuration;
        [SetUp] public void SetUp() => _configuration = ScriptableObject.CreateInstance<RaceVfxDefinition>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_configuration);

        [TestCase(0f, 0f, 0f, 0f, 1f, false, false)]
        [TestCase(20f, 0f, 0f, 0f, 0f, false, false)]
        [TestCase(20f, 0f, 0f, 0f, 0.2f, false, false)]
        [TestCase(20f, 0f, 0f, 0.5f, 1f, false, true)]
        [TestCase(20f, 22f, 0f, 0f, 0f, false, true)]
        [TestCase(20f, 0f, 0.6f, 0f, 0f, false, true)]
        [TestCase(20f, 0f, 0f, 0f, 0f, true, true)]
        public void MarksRequireMotionAndHardBrakingOrActualSlide(float speed, float angle, float sideSlip,
            float forwardSlip, float braking, bool rearHandbrake, bool expected)
        {
            var intensity = RaceVfxRules.SkidIntensity(speed, angle, sideSlip, forwardSlip, braking, rearHandbrake, _configuration);
            Assert.That(intensity > 0f, Is.EqualTo(expected));
            Assert.That(intensity, Is.InRange(0f, 1f));
        }

#if UNITY_EDITOR
        [Test]
        public void TireMaterialReferencesACompiledUrpShader()
        {
            var definition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(
                "Assets/ApexRivals/Content/Configuration/VFX/RaceVfxDefinition.asset");
            Assert.That(definition.skidmarkMaterial, Is.Not.Null);
            Assert.That(definition.skidmarkMaterial.shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(definition.skidmarkMaterial.shader), Is.False);
        }
#endif
    }
}
