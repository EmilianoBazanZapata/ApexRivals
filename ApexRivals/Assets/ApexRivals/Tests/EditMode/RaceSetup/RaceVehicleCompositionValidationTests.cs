using System.Collections.Generic;
using ApexRivals.AI.Runtime;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.RaceSetup
{
    public sealed class RaceVehicleCompositionValidationTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _createdObjects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ValidPlayerComposition_PassesValidation()
        {
            var setup = CreateValidComposition();

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.True, message);
        }

        [Test]
        public void ValidAiComposition_PassesValidation()
        {
            var setup = CreateValidComposition();

            var valid = setup.Composition.TryValidateAI(out var message);

            Assert.That(valid, Is.True, message);
        }

        [TestCase("Assets/ApexRivals/Vehicle/Prefabs/Vehicle_Vanguard.prefab")]
        [TestCase("Assets/ApexRivals/Vehicle/Prefabs/Vehicle_Striker.prefab")]
        public void ProductionVehicle_HasConfiguredRoofRecoveryDetector(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null, prefabPath);
            var detector = prefab.GetComponent<VehicleRoofRecoveryDetector>();
            Assert.That(detector, Is.Not.Null, prefabPath);
            Assert.That(detector.TryValidateConfiguration(out var message), Is.True, message);
        }

        [Test]
        public void MissingVehicleRuntime_FailsWithExactRuntimeMessage()
        {
            var setup = CreateValidComposition(false);

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("exactly one IVehicleRuntime"));
            Assert.That(message, Does.Contain("found 0"));
        }

        [Test]
        public void MultipleVehicleRuntimes_FailsAsAmbiguous()
        {
            var setup = CreateValidComposition();
            setup.Root.AddComponent<TestVehicleRuntime>();

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("exactly one IVehicleRuntime"));
            Assert.That(message, Does.Contain("found 2"));
        }

        [Test]
        public void InvalidSerializedRuntimeComponent_FailsClearly()
        {
            var setup = CreateValidComposition();
            var invalidReference = setup.Root.AddComponent<InvalidRuntimeReference>();
            setup.Composition.ConfigureReferences(
                invalidReference,
                setup.Resetter,
                setup.Participant,
                setup.Gate,
                setup.PlayerInput,
                setup.AiInput,
                setup.Root.transform,
                setup.Root.transform,
                setup.Root.transform,
                null);

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("does not implement IVehicleRuntime"));
            Assert.That(message, Does.Contain(nameof(InvalidRuntimeReference)));
        }

        [Test]
        public void MissingVehicleResetter_FailsClearly()
        {
            var setup = CreateValidComposition();
            setup.Composition.ConfigureReferences(
                setup.Runtime,
                null,
                setup.Participant,
                setup.Gate,
                setup.PlayerInput,
                setup.AiInput,
                setup.Root.transform,
                setup.Root.transform,
                setup.Root.transform,
                null);

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("requires VehicleResetter"));
        }

        [Test]
        public void MissingDrivingInputGate_FailsClearly()
        {
            var setup = CreateValidComposition();
            setup.Composition.ConfigureReferences(
                setup.Runtime,
                setup.Resetter,
                setup.Participant,
                null,
                setup.PlayerInput,
                setup.AiInput,
                setup.Root.transform,
                setup.Root.transform,
                setup.Root.transform,
                null);

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("requires DrivingInputGate"));
        }

        [Test]
        public void MissingRaceParticipant_FailsClearly()
        {
            var setup = CreateValidComposition();
            setup.Composition.ConfigureReferences(
                setup.Runtime,
                setup.Resetter,
                null,
                setup.Gate,
                setup.PlayerInput,
                setup.AiInput,
                setup.Root.transform,
                setup.Root.transform,
                setup.Root.transform,
                null);

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("requires RaceParticipant"));
        }

        [Test]
        public void PlayerWithoutPlayerDrivingInput_FailsClearly()
        {
            var setup = CreateValidComposition();
            setup.Composition.ConfigureReferences(
                setup.Runtime,
                setup.Resetter,
                setup.Participant,
                setup.Gate,
                null,
                setup.AiInput,
                setup.Root.transform,
                setup.Root.transform,
                setup.Root.transform,
                null);

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("requires PlayerDrivingInput"));
        }

        [Test]
        public void AiWithoutAiDrivingInputProvider_FailsClearly()
        {
            var setup = CreateValidComposition();
            setup.Composition.ConfigureReferences(
                setup.Runtime,
                setup.Resetter,
                setup.Participant,
                setup.Gate,
                setup.PlayerInput,
                null,
                setup.Root.transform,
                setup.Root.transform,
                setup.Root.transform,
                null);

            var valid = setup.Composition.TryValidateAI(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("requires AiDrivingInputProvider"));
        }

        [Test]
        public void PlayerWithoutCameraTarget_FailsClearly()
        {
            var setup = CreateValidComposition();
            setup.Composition.ConfigureReferences(
                setup.Runtime,
                setup.Resetter,
                setup.Participant,
                setup.Gate,
                setup.PlayerInput,
                setup.AiInput,
                null,
                setup.Root.transform,
                setup.Root.transform,
                null);

            var valid = setup.Composition.TryValidatePlayer(out var message);

            Assert.That(valid, Is.False);
            Assert.That(message, Does.Contain("requires a CameraTarget"));
        }

        private CompositionSetup CreateValidComposition(bool includeRuntime = true)
        {
            var root = new GameObject("CompositionVehicle");
            _createdObjects.Add(root);
            root.AddComponent<Rigidbody>();

            var runtime = includeRuntime ? root.AddComponent<TestVehicleRuntime>() : null;
            var resetter = root.AddComponent<VehicleResetter>();
            var resetterSerializedObject = new SerializedObject(resetter);
            resetterSerializedObject.FindProperty("vehicleRigidbody").objectReferenceValue = root.GetComponent<Rigidbody>();
            resetterSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            var participant = root.AddComponent<RaceParticipant>();
            var gate = root.AddComponent<DrivingInputGate>();
            var playerInput = root.AddComponent<PlayerDrivingInput>();
            var aiInput = root.AddComponent<AiDrivingInputProvider>();
            var composition = root.AddComponent<RaceVehicleComposition>();
            composition.ConfigureReferences(
                runtime,
                resetter,
                participant,
                gate,
                playerInput,
                aiInput,
                root.transform,
                root.transform,
                root.transform,
                null);

            return new CompositionSetup(root, runtime, resetter, participant, gate, playerInput, aiInput, composition);
        }

        private readonly struct CompositionSetup
        {
            public CompositionSetup(
                GameObject root,
                TestVehicleRuntime runtime,
                VehicleResetter resetter,
                RaceParticipant participant,
                DrivingInputGate gate,
                PlayerDrivingInput playerInput,
                AiDrivingInputProvider aiInput,
                RaceVehicleComposition composition)
            {
                Root = root;
                Runtime = runtime;
                Resetter = resetter;
                Participant = participant;
                Gate = gate;
                PlayerInput = playerInput;
                AiInput = aiInput;
                Composition = composition;
            }

            public GameObject Root { get; }
            public TestVehicleRuntime Runtime { get; }
            public VehicleResetter Resetter { get; }
            public RaceParticipant Participant { get; }
            public DrivingInputGate Gate { get; }
            public PlayerDrivingInput PlayerInput { get; }
            public AiDrivingInputProvider AiInput { get; }
            public RaceVehicleComposition Composition { get; }
        }

        private sealed class TestVehicleRuntime : MonoBehaviour, IVehicleRuntime
        {
            public float CurrentForwardSpeed => 0f;
            public VehiclePerformanceStats CurrentPerformanceStats => default;

            public void ConfigureRuntime(IDrivingInputProvider inputProvider, VehicleResetter vehicleResetter)
            {
            }

            public void ApplyPerformanceStats(VehiclePerformanceStats performanceStats)
            {
            }
        }

        private sealed class InvalidRuntimeReference : MonoBehaviour
        {
        }
    }
}
