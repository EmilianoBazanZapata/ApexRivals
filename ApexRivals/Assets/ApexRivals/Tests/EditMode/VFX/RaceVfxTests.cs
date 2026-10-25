using ApexRivals.VFX.Runtime;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ApexRivals.Tests.EditMode.VFX
{
    public sealed class RaceVfxTests
    {
        [TestCase(12f, 20f, 0f, true, true, true)]
        [TestCase(12f, 0f, 0.5f, true, true, true)]
        [TestCase(12f, 0f, 0f, true, true, false)]
        [TestCase(2f, 30f, 1f, true, true, false)]
        [TestCase(12f, 30f, 1f, false, true, false)]
        [TestCase(12f, 30f, 1f, true, false, false)]
        public void SmokeRequiresSpeedSlipGroundContactAndDriving(float speed, float angle, float wheelSlip,
            bool grounded, bool driving, bool expected)
        {
            Assert.That(RaceVfxRules.ShouldSmoke(speed, angle, wheelSlip, grounded, driving, 6f, 9f, 0.25f), Is.EqualTo(expected));
        }

        [Test]
        public void ImpactsUseClosingSpeedAndIgnoreGroundLandings()
        {
            Assert.That(RaceVfxRules.ImpactStrength(Vector3.forward * 12f, Vector3.back, false), Is.EqualTo(12f));
            Assert.That(RaceVfxRules.ImpactStrength(Vector3.down * 20f, Vector3.up, false), Is.Zero);
            Assert.That(RaceVfxRules.ImpactStrength(Vector3.right, Vector3.forward, false), Is.Zero);
        }

        [Test]
        public void WallScrapesRequireSubstantialTangentialSpeed()
        {
            Assert.That(RaceVfxRules.ImpactStrength(Vector3.right * 20f, Vector3.forward, true), Is.GreaterThan(2f));
            Assert.That(RaceVfxRules.ImpactStrength(Vector3.right * 2f, Vector3.forward, true), Is.Zero);
        }

        [TestCase(true, 1, true)]
        [TestCase(true, 2, false)]
        [TestCase(false, 1, false)]
        public void OnlyThePlayersWinCelebrates(bool isPlayer, int position, bool expected)
        {
            Assert.That(RaceVfxRules.ShouldCelebrate(isPlayer, position), Is.EqualTo(expected));
        }

#if UNITY_EDITOR
        [Test]
        public void AuthoredAssetsReferenceSupportedShadersAndBoundedParticles()
        {
            var definition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(
                "Assets/ApexRivals/Content/Configuration/VFX/RaceVfxDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            foreach (var prefab in new[] { definition.driftSmokePrefab, definition.impactSparksPrefab, definition.rainPrefab })
            {
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(prefab.main.maxParticles, Is.LessThanOrEqualTo(1200));
                var shader = prefab.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader;
                Assert.That(shader.isSupported, Is.True, shader.name);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, shader.name);
            }
            Assert.That(definition.confettiMaterial.shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(definition.confettiMaterial.shader), Is.False);
            var smokeMesh = definition.driftSmokePrefab.GetComponent<ParticleSystemRenderer>().mesh;
            Assert.That(smokeMesh.triangles.Length, Is.EqualTo(24), "Smoke uses eight hard-edged faces.");
        }
#endif
    }
}
