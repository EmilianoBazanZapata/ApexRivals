using System.Reflection;
using ApexRivals.Camera.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.Tests.EditMode
{
    public sealed class CarPresentationAuthoringTests
    {
        [TestCase("Race")]
        [TestCase("RaceDunes")]
        [TestCase("RaceSuzuka")]
        public void EveryCircuitHasLoopingPlayerShotsAndAnAuthoredClickableStartButton(string name)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/ApexRivals/Scenes/" + name + ".unity");
            try
            {
                PreRacePresentation intro = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (intro == null) intro = root.GetComponentInChildren<PreRacePresentation>(true);
                Assert.That(intro, Is.Not.Null);
                var refs = new SerializedObject(intro);
                Assert.That(refs.FindProperty("waitForStart").boolValue, Is.True);
                var shots = refs.FindProperty("carShots");
                Assert.That(shots.arraySize, Is.EqualTo(3));
                for (var i = 0; i < shots.arraySize; i++)
                {
                    var shot = shots.GetArrayElementAtIndex(i);
                    Assert.That(shot.FindPropertyRelative("camera").objectReferenceValue, Is.Not.Null);
                    Assert.That(shot.FindPropertyRelative("seconds").floatValue, Is.GreaterThan(.1f));
                    Assert.That(shot.FindPropertyRelative("startOffset").vector3Value,
                        Is.Not.EqualTo(shot.FindPropertyRelative("endOffset").vector3Value));
                }
                var hint = refs.FindProperty("introductionHint").objectReferenceValue as GameObject;
                var button = refs.FindProperty("startRaceButton").objectReferenceValue as Button;
                Assert.That(hint, Is.Not.Null);
                Assert.That(button, Is.Not.Null);
                Assert.That(button.transform.IsChildOf(hint.transform), Is.True);
                Assert.That(hint.GetComponent<GraphicRaycaster>(), Is.Not.Null);
                var fade = refs.FindProperty("shotFade").objectReferenceValue as CanvasGroup;
                Assert.That(refs.FindProperty("fadeBetweenShots").boolValue, Is.True);
                Assert.That(fade, Is.Not.Null);
                Assert.That(fade.alpha, Is.Zero);
                Assert.That(fade.blocksRaycasts, Is.False);
                Assert.That(fade.interactable, Is.False);
                Assert.That(fade.gameObject.activeSelf, Is.False);
                var cover = fade.GetComponentInChildren<Image>(true);
                Assert.That(cover, Is.Not.Null);
                Assert.That(cover.raycastTarget, Is.False);
                Assert.That(cover.color, Is.EqualTo(Color.black));
                Assert.That(fade.GetComponent<Canvas>().sortingOrder, Is.LessThan(hint.GetComponent<Canvas>().sortingOrder));
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero, "The runtime owner binds the start command once.");
                Assert.That(button.GetComponentInChildren<Text>(true).text, Is.EqualTo("START RACE"));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void CarFocusUsesVisibleCarMeshesAndIgnoresDisabledMeshesAndParticleBounds()
        {
            var root = new GameObject("CarFocusTest");
            try
            {
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
                car.transform.SetParent(root.transform);
                car.transform.position = new Vector3(4, 1, 6);
                var disabled = GameObject.CreatePrimitive(PrimitiveType.Cube);
                disabled.transform.SetParent(root.transform);
                disabled.transform.position = Vector3.one * 100;
                disabled.GetComponent<MeshRenderer>().enabled = false;
                var smoke = new GameObject("Smoke", typeof(ParticleSystem));
                smoke.transform.SetParent(root.transform);
                smoke.transform.position = Vector3.one * 200;
                Assert.That(Centre(body), Is.EqualTo(car.GetComponent<Renderer>().bounds.center));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CarFocusFallsBackToAuthoredCentreOfMassWhenThereIsNoVisibleMesh()
        {
            var root = new GameObject("CarFocusFallback");
            try
            {
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.centerOfMass = new Vector3(.3f, .8f, -.2f);
                Assert.That(Centre(body), Is.EqualTo(body.worldCenterOfMass));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Vector3 Centre(Rigidbody body) => (Vector3)typeof(PreRacePresentation)
            .GetMethod("FindCarCentre", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { body });
    }
}
