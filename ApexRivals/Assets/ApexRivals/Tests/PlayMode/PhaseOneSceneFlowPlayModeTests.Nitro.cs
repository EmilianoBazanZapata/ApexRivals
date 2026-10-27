#if UNITY_EDITOR
using System.Collections;
using ApexRivals.AI.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ApexRivals.Tests.PlayMode
{
    public sealed partial class PhaseOneSceneFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator Nitro_PlayerAndHudAreWiredAcrossAllTracksAndRetryStartsEmpty()
        {
            _installAllContentScenes = true;
            foreach (var track in new[] { "Race", "RaceDunes", "RaceSuzuka" })
            {
                Assert.That(_flow.SelectRaceScene(track), Is.True);
                var retry = _flow.RetryRace();
                yield return WaitForTask(retry);
                Assert.That(retry.Result.Succeeded, Is.True, retry.Result.Message);
                var installer = FindInScene<RaceSessionSceneInstaller>(track);
                var nitro = installer.Session.PlayerVehicleRigidbody.GetComponent<PlayerVehicleNitro>();
                Assert.That(nitro, Is.Not.Null, track);
                Assert.That(nitro.enabled, Is.True);
                Assert.That(nitro.NormalizedCharge, Is.Zero);
                var hud = FindInScene<RaceHudUguiView>(track);
                var panel = hud.GetComponentInChildren<NitroHudUguiView>(true);
                Assert.That(panel, Is.Not.Null, track);
                yield return null;
                Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                Assert.That(panel.transform.Find("NitroCharge").GetComponent<Text>().text, Is.EqualTo("0%"));
                foreach (var root in SceneManager.GetSceneByName(track).GetRootGameObjects())
                    foreach (var rival in root.GetComponentsInChildren<AiDrivingInputProvider>(true))
                        if (rival.gameObject != nitro.gameObject && rival.enabled)
                            Assert.That(rival.GetComponent<PlayerVehicleNitro>(), Is.Null, "AI must retain its existing race tuning.");
                ScreenCapture.CaptureScreenshot("graphify-out/nitro-hud-" + track + ".png");
                yield return null;
            }
        }
    }
}
#endif
