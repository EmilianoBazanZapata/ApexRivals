#if UNITY_EDITOR
using System.Collections;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.RaceSession.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Fog_SuzukaPersistsThroughIntroductionPauseRetryAndClearsOnSceneChange()
        {
            _playIntroduction = true;
            _installAllContentScenes = true;
            _flow.SelectRaceScene("RaceSuzuka");
            yield return WaitForTask(_flow.RetryRace());
            var installer = FindInScene<RaceSessionSceneInstaller>("RaceSuzuka");
            AssertSuzukaFog();
            yield return new WaitForSeconds(0.7f);
            ScreenCapture.CaptureScreenshot("graphify-out/fog-suzuka-intro.png");
            yield return null;
            installer.Introduction.RequestSkip();
            var deadline = Time.realtimeSinceStartup + 6;
            while ((installer.Session.State == RaceSessionLifecycleState.Ready || installer.Session.State == RaceSessionLifecycleState.Starting)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(installer.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
            yield return new WaitForSeconds(1.2f);
            ScreenCapture.CaptureScreenshot("graphify-out/fog-suzuka-gameplay.png");
            yield return null;
            Assert.That(installer.Session.Pause().Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            AssertSuzukaFog();
            Assert.That(installer.Session.Resume().Succeeded, Is.True);
            yield return WaitForTask(_flow.RetryRace());
            AssertSuzukaFog();

            _playIntroduction = false;
            foreach (var track in new[] { "Race", "RaceDunes" })
            {
                _bootstrapper.Context.SelectedVehicleState.Commit(track == "RaceDunes" ? "striker" : "vanguard");
                _flow.SelectRaceScene(track);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                Assert.That(RenderSettings.fog, Is.False, "Suzuka fog must not leak into " + track);
                Assert.That(RenderSettings.skybox.name, Is.Not.EqualTo("SuzukaOvercastSky"));
            }
            yield return WaitForTask(_flow.OpenMainMenu());
            Assert.That(RenderSettings.fog, Is.False);
            yield return WaitForTask(_flow.OpenGarage());
            Assert.That(RenderSettings.fog, Is.False);
        }

        [UnityTest]
        public IEnumerator Fog_RenderedNearObjectStaysClearAndDistantObjectFades()
        {
            _flow.SelectRaceScene("RaceSuzuka");
            yield return WaitForTask(_flow.RetryRace());
            AssertSuzukaFog();
            var follow = FindInScene<WheelVehicleFollowCamera>("RaceSuzuka");
            follow.enabled = false;
            var camera = follow.GetComponent<UnityEngine.Camera>();
            var texture = new RenderTexture(256, 256, 24);
            var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.black);
            var near = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var far = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                // Isolated above the real track, using the actual scene fog and URP camera.
                camera.transform.SetPositionAndRotation(new Vector3(0, 1500, 0), Quaternion.identity);
                camera.targetTexture = texture;
                camera.aspect = 1;
                near.transform.position = camera.transform.position + new Vector3(-4, 0, 20);
                near.transform.localScale = Vector3.one * 3;
                far.transform.position = camera.transform.position + new Vector3(48, 0, 240);
                far.transform.localScale = Vector3.one * 36;
                near.GetComponent<Renderer>().sharedMaterial = material;
                far.GetComponent<Renderer>().sharedMaterial = material;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                pixels.Apply();
                var nearPoint = camera.WorldToViewportPoint(near.transform.position);
                var farPoint = camera.WorldToViewportPoint(far.transform.position);
                var nearColor = pixels.GetPixel(Mathf.RoundToInt(nearPoint.x * 255), Mathf.RoundToInt(nearPoint.y * 255));
                var farColor = pixels.GetPixel(Mathf.RoundToInt(farPoint.x * 255), Mathf.RoundToInt(farPoint.y * 255));
                Assert.That(nearColor.grayscale, Is.LessThan(0.15f), "Nearby black geometry retains its contrast.");
                Assert.That(farColor.grayscale - nearColor.grayscale, Is.GreaterThan(0.25f), "URP must actually blend distant geometry into fog.");
                System.IO.File.WriteAllBytes("graphify-out/fog-depth-readback.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                Object.Destroy(near); Object.Destroy(far); Object.Destroy(material); Object.Destroy(pixels);
                texture.Release(); Object.Destroy(texture);
            }
        }

        private static void AssertSuzukaFog()
        {
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
            Assert.That(RenderSettings.fogStartDistance, Is.GreaterThanOrEqualTo(40), "Keep the immediate driving view clear.");
            Assert.That(RenderSettings.fogEndDistance, Is.InRange(250f, 450f), "Keep enough visibility to anticipate corners.");
            Assert.That(RenderSettings.skybox.name, Is.EqualTo("SuzukaOvercastSky"));
            var horizon = RenderSettings.skybox.GetColor("_HorizonColor");
            var fog = RenderSettings.fogColor;
            for (var channel = 0; channel < 4; channel++) Assert.That(horizon[channel], Is.EqualTo(fog[channel]).Within(0.0001f));
            Assert.That(RenderSettings.skybox.shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(RenderSettings.skybox.shader), Is.False);
        }
    }
}
#endif
