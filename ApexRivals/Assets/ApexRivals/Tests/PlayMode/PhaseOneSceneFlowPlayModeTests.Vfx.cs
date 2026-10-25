#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.VFX.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Vfx_PhysicalSlideEmitsGroundedSmokeAndPauseStopsEmission()
        {
            var body = Installer.Session.PlayerVehicleRigidbody;
            var effects = body.GetComponent<VehicleRaceVfx>();
            Assert.That(effects, Is.Not.Null);
            Assert.That(effects.SmokeParticlesEmitted, Is.Zero);
            for (var i = 0; i < 18; i++)
            {
                body.linearVelocity = body.transform.forward * 14f + body.transform.right * 8f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(effects.SmokeParticlesEmitted, Is.GreaterThan(0), "Real wheel contacts and lateral motion emit smoke.");
            ScreenCapture.CaptureScreenshot("graphify-out/vfx-drift.png");
            yield return null;
            Assert.That(Installer.Session.Pause().Succeeded, Is.True);
            var pausedCount = effects.SmokeParticlesEmitted;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(effects.SmokeParticlesEmitted, Is.EqualTo(pausedCount));
            Installer.Session.Resume();
            body.position += Vector3.up * 8f;
            body.linearVelocity = body.transform.forward * 14f + body.transform.right * 8f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var airborneCount = effects.SmokeParticlesEmitted;
            yield return new WaitForSeconds(0.15f);
            Assert.That(effects.SmokeParticlesEmitted, Is.EqualTo(airborneCount), "Airborne wheels cannot leave smoke on the road.");
        }

        [UnityTest]
        public IEnumerator Vfx_WallCollisionEmitsSparksAndRespectsCooldown()
        {
            var body = Installer.Session.PlayerVehicleRigidbody;
            var effect = body.GetComponent<VehicleRaceVfx>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "VfxTestWall";
            wall.transform.SetPositionAndRotation(body.position + body.transform.forward * 5f + Vector3.up, body.rotation);
            wall.transform.localScale = new Vector3(8f, 3f, 0.5f);
            try
            {
                body.linearVelocity = body.transform.forward * 18f;
                var deadline = Time.realtimeSinceStartup + 1.5f;
                while (effect.ImpactBurstsEmitted == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(effect.ImpactBurstsEmitted, Is.GreaterThan(0), "A real chassis/wall collision creates a burst.");
                Assert.That(effect.LastImpactWasVehicle, Is.False);
                Assert.That(effect.EmitImpact(body.position, Vector3.back, 12f, true), Is.False, "Repeated contact in one frame is throttled.");
                yield return new WaitForSeconds(0.04f);
                ScreenCapture.CaptureScreenshot("graphify-out/vfx-wall-impact.png");
                yield return new WaitForSeconds(0.2f);
                Assert.That(effect.EmitImpact(body.position + Vector3.up, Vector3.up + Vector3.right, 12f, true), Is.True);
                Assert.That(effect.LastImpactWasVehicle, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); }
        }

        [UnityTest]
        public IEnumerator Vfx_CarToCarContactEmitsOneSharedBurst()
        {
            var coordinator = FindInRace<RaceCoordinator>();
            var player = Installer.Session.PlayerVehicleRigidbody;
            Rigidbody rival = null;
            foreach (var racer in coordinator.Participants)
            {
                var body = racer.GetComponentInParent<Rigidbody>();
                if (body == player) continue;
                if (rival == null) rival = body;
                else body.position += Vector3.right * 50f;
            }
            Assert.That(rival, Is.Not.Null);
            var playerEffect = player.GetComponent<VehicleRaceVfx>();
            var rivalEffect = rival.GetComponent<VehicleRaceVfx>();
            rival.position = player.position + player.transform.forward * 5f;
            rival.rotation = player.rotation;
            player.linearVelocity = player.transform.forward * 12f;
            rival.linearVelocity = -player.transform.forward * 12f;
            var before = playerEffect.ImpactBurstsEmitted + rivalEffect.ImpactBurstsEmitted;
            var deadline = Time.realtimeSinceStartup + 1.5f;
            while (playerEffect.ImpactBurstsEmitted + rivalEffect.ImpactBurstsEmitted == before
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(playerEffect.ImpactBurstsEmitted + rivalEffect.ImpactBurstsEmitted, Is.EqualTo(before + 1),
                "Reciprocal collision callbacks produce one burst, not two.");
            Assert.That(playerEffect.LastImpactWasVehicle || rivalEffect.LastImpactWasVehicle, Is.True);
        }

        [UnityTest]
        public IEnumerator Vfx_RainIsTrackSpecificBoundedAndUnloadedWithTheRace()
        {
            var dry = FindInRace<RaceVfxSceneInstaller>();
            Assert.That(dry.RainEnabled, Is.False);
            Assert.That(dry.Rain, Is.Null);
            _installAllContentScenes = true;
            _flow.SelectRaceScene("RaceSuzuka");
            yield return WaitForTask(_flow.RetryRace());
            var wet = FindInScene<RaceVfxSceneInstaller>("RaceSuzuka");
            Assert.That(wet.RainEnabled, Is.True);
            var rain = wet.Rain;
            yield return new WaitForSeconds(1.6f); // Let the scene's existing follow camera settle from its authored pose.
            Assert.That(rain.particleCount, Is.GreaterThan(0));
            Assert.That(rain.particleCount, Is.LessThanOrEqualTo(1200));
            var drops = new ParticleSystem.Particle[1];
            rain.GetParticles(drops);
            Assert.That(drops[0].velocity.y, Is.LessThan(-1f), "Rain falls down in world space.");
            ScreenCapture.CaptureScreenshot("graphify-out/vfx-rain.png");
            _installAllContentScenes = false;
            yield return WaitForTask(_flow.ForceReturnToMainMenu());
            yield return null;
            Assert.That(rain == null, Is.True, "Weather is owned by its race scene.");
        }

        [UnityTest]
        public IEnumerator Vfx_PlayerWinShowsConfettiOnceAboveResultsAndWhileTimeIsPaused()
        {
            var effects = FindInRace<RaceVfxSceneInstaller>();
            Assert.That(effects.TryCelebrate(2), Is.False);
            CompleteStabilityRace();
            Assert.That(_flow.RaceSession.Result.FinalPosition, Is.EqualTo(1));
            Assert.That(effects.HasCelebrated, Is.True);
            Assert.That(effects.TryCelebrate(1), Is.False);
            Assert.That(effects.Confetti.raycastTarget, Is.False);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(effects.Confetti.IsCelebrating, Is.True);
            Assert.That(effects.Confetti.rectTransform.rect.width, Is.GreaterThan(100f));
            var confettiMesh = effects.Confetti.canvasRenderer.GetMesh();
            Assert.That(confettiMesh.vertexCount, Is.GreaterThan(0));
            ScreenCapture.CaptureScreenshot("graphify-out/vfx-victory.png");
            yield return new WaitForSecondsRealtime(3.4f);
            Assert.That(effects.Confetti.IsCelebrating, Is.False);
            Time.timeScale = 1f;
        }
    }
}
#endif
