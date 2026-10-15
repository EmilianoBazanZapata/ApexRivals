#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ApexRivals.Camera.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class PhaseFourIntegrationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = SceneManager.CreateScene("PhaseFourPhysics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in _objects)
                if (obj != null) Object.DestroyImmediate(obj);
            _objects.Clear();
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [TestCase("Vanguard", 50f)]
        [TestCase("Striker", 50f)]
        [TestCase("Vanguard", 100f)]
        [TestCase("Striker", 100f)]
        public void VehicleCannotCrossThinWallAtHighSpeed(string vehicle, float speed)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ApexRivals/Vehicle/Prefabs/Vehicle_" + vehicle + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab, new Vector3(0f, 4f, 0f), Quaternion.identity);
            _objects.Add(root);
            SceneManager.MoveGameObjectToScene(root, _scene);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var body = root.GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            body.useGravity = false;
            var wall = Cube("ThinWall", new Vector3(speed * 0.01f, 4f, 0f), new Vector3(0.05f, 20f, 20f));
            // Put the leading side just before the wall; the end pose would be beyond it.
            var chassis = root.GetComponentInChildren<BoxCollider>();
            Assert.That(chassis, Is.Not.Null);
            var leadingExtent = chassis.bounds.max.x - root.transform.position.x;
            root.transform.position = new Vector3(wall.transform.position.x - leadingExtent - 0.05f, 4f, 0f);
            body.linearVelocity = Vector3.right * speed;
            Physics.SyncTransforms();
            for (var i = 0; i < 10; i++) _scene.GetPhysicsScene().Simulate(0.02f);
            Assert.That(body.position.x, Is.LessThan(wall.transform.position.x), "Chassis tunneled through the wall at " + speed + " m/s");
        }

        [UnityTest]
        public IEnumerator CameraStaysOnVehicleSideOfWall()
        {
            var vehicle = Cube("Vehicle", Vector3.zero, Vector3.one);
            var body = vehicle.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var cameraObject = new GameObject("FollowCamera");
            _objects.Add(cameraObject);
            SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            cameraObject.AddComponent<UnityEngine.Camera>();
            var follow = cameraObject.AddComponent<WheelVehicleFollowCamera>();
            follow.SetTarget(vehicle.transform);
            follow.SetVehicleRigidbody(body);
            Cube("Occluder", new Vector3(0f, 1f, -2.5f), new Vector3(10f, 8f, 0.2f));
            Physics.SyncTransforms();
            for (var i = 0; i < 50; i++) yield return null;
            Assert.That(cameraObject.transform.position.z, Is.GreaterThan(-2.2f), "Camera moved behind the solid wall");
        }

        [UnityTest]
        public IEnumerator CameraHandlesReverseRecoveryAndIgnoresTriggersAndOwnChassis()
        {
            var vehicle = Cube("Vehicle", Vector3.zero, new Vector3(2f, 2f, 4f));
            var body = vehicle.AddComponent<Rigidbody>();
            body.useGravity = false;
            var cameraObject = new GameObject("FollowCamera");
            _objects.Add(cameraObject);
            SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            var lens = cameraObject.AddComponent<UnityEngine.Camera>();
            lens.nearClipPlane = 0.3f;
            var follow = cameraObject.AddComponent<WheelVehicleFollowCamera>();
            follow.SetTarget(vehicle.transform);
            follow.SetVehicleRigidbody(body);
            var trigger = Cube("Checkpoint", new Vector3(0f, 1f, -2.5f), new Vector3(10f, 8f, 1f));
            trigger.GetComponent<Collider>().isTrigger = true;
            yield return null;
            for (var i = 0; i < 80; i++) yield return null;
            Assert.That(cameraObject.transform.position.z, Is.LessThan(-4f), "Own body and checkpoint trigger must not obstruct the camera");
            var wall = Cube("ReverseWall", new Vector3(0f, 1f, 2.5f), new Vector3(10f, 8f, 0.2f));
            Physics.SyncTransforms();
            body.linearVelocity = Vector3.back * 2f;
            follow.SendMessage("LateUpdate");
            Assert.That(follow.ReverseCameraActive, Is.True);
            // Simulate a recovery that leaves the previous camera pose behind a wall.
            cameraObject.transform.position = new Vector3(0f, 2f, 5f);
            follow.SendMessage("LateUpdate");
            Assert.That(cameraObject.transform.position.z, Is.LessThan(2.2f));
            Assert.That(cameraObject.transform.position.z, Is.GreaterThan(0f));
            Object.DestroyImmediate(wall);
        }

        [TestCase(50f)]
        [TestCase(100f)]
        public void HighSpeedCheckpointCrossingIsRecordedExactlyOnce(float speed)
        {
            var owner = new GameObject("RaceCoordinator");
            _objects.Add(owner);
            SceneManager.MoveGameObjectToScene(owner, _scene);
            owner.SetActive(false);
            var coordinator = owner.AddComponent<RaceCoordinator>();
            Set(coordinator, "startCountdownOnEnable", false);
            Set(coordinator, "initiallyConsumedCheckpointIndex", -1);
            var checkpointObject = Cube("Checkpoint", Vector3.zero, Vector3.one);
            var box = checkpointObject.GetComponent<BoxCollider>();
            box.size = new Vector3(8f, 4f, 3f);
            box.isTrigger = true;
            var checkpoint = checkpointObject.AddComponent<RaceCheckpoint>();
            Set(checkpoint, "raceCoordinator", coordinator);
            Set(coordinator, "orderedCheckpoints", new[] { checkpoint });
            owner.SetActive(true);
            coordinator.enabled = false;
            var car = Cube("Player", new Vector3(0f, 0f, -5f), new Vector3(1.6f, 1f, 4.35f));
            var body = car.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var participant = car.AddComponent<RaceParticipant>();
            participant.Configure(coordinator, "Player", car.transform);
            Set(coordinator, "state", RaceState.Racing);
            var events = 0;
            coordinator.CheckpointPassed += _ => events++;
            Physics.SyncTransforms();
            body.linearVelocity = Vector3.forward * speed;
            for (var i = 0; i < 20; i++) _scene.GetPhysicsScene().Simulate(0.02f);
            Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
            Assert.That(progress.TotalPassedCheckpoints, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(1));
        }

        private static void Set(object owner, string field, object value) => owner.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

        private GameObject Cube(string name, Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            _objects.Add(obj);
            SceneManager.MoveGameObjectToScene(obj, _scene);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            return obj;
        }
    }
}
#endif
