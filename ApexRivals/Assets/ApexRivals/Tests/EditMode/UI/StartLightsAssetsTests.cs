using System.Linq;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class StartLightsAssetsTests
    {
        private const string Folder = "Assets/ApexRivals/UI/StartLights/";

        [Test]
        public void PrefabHasAllPresentationReferencesAndFiveEditableAnimatorStates()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RaceStartLights.prefab");
            Assert.That(prefab, Is.Not.Null);
            var view = new SerializedObject(prefab.GetComponent<RaceStartLights>());
            foreach (var reference in new[] { "display", "animator", "label", "audioSource", "countdownCue", "goCue" })
                Assert.That(view.FindProperty(reference).objectReferenceValue, Is.Not.Null, reference);
            var source = prefab.GetComponent<AudioSource>();
            Assert.That(source.playOnAwake, Is.False);
            Assert.That(source.spatialBlend, Is.Zero);
            Assert.That(prefab.GetComponent<Canvas>().enabled, Is.False, "Hidden during loading and the introduction.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "StartLights.controller");
            Assert.That(controller.layers[0].stateMachine.states.Select(s => s.state.name),
                Is.EquivalentTo(new[] { "Idle", "Three", "Two", "One", "Go" }));
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                var clip = state.state.motion as AnimationClip;
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.isLooping, Is.False);
                Assert.That(clip.length, Is.GreaterThanOrEqualTo(1));
                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var lamp in new[] { "Panel/Lamp0", "Panel/Lamp1", "Panel/Lamp2" })
                    Assert.That(bindings.Any(b => b.path == lamp && b.propertyName == "m_Color.r"), Is.True);
                Assert.That(bindings.Any(b => b.propertyName == "m_LocalScale.x"), Is.True);
                Assert.That(bindings.Any(b => b.propertyName == "m_Alpha"), Is.True);
            }
        }
    }
}
