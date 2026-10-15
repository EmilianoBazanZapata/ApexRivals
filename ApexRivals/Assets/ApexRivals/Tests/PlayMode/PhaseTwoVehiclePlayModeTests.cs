#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ApexRivals.Garage.Configuration;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Configuration;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ApexRivals.Tests.PlayMode
{
    public sealed class PhaseTwoVehiclePlayModeTests
    {
        private Scene _scene;
        private PhysicsScene _physics;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private const float Step = 0.02f;

        private sealed class Input : IDrivingInputProvider
        {
            public DrivingInput CurrentInput { get; set; }
            public void ConsumeResetRequest() => CurrentInput = CurrentInput.WithoutResetRequest();
            public void ConsumeRecoveryRequest() => CurrentInput = CurrentInput.WithoutRecoveryRequest();
        }

        private sealed class Car
        {
            public GameObject Root;
            public Rigidbody Body;
            public WheelArcadeVehicleController Controller;
            public VehicleResetter Resetter;
            public Input Input = new Input();
        }

        [SetUp]
        public void SetUp()
        {
            _objects.Clear();
            _scene = SceneManager.CreateScene("PhaseTwoVehiclePhysics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            _physics = _scene.GetPhysicsScene();
            var floor = CreatePrimitive("Road", new Vector3(0f, -0.5f, 500f), new Vector3(1000f, 1f, 2000f));
            Assert.That(floor.GetComponent<Collider>(), Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in _objects)
                if (obj != null) Object.DestroyImmediate(obj);
            _objects.Clear();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(_scene);
                if (unload != null) yield return unload;
            }
        }

        [UnityTest]
        public IEnumerator GaragePurchasesChangeTorqueSteeringAndTireGripWithoutCompounding()
        {
            var car = CreateCar(0f);
            var baseline = car.Controller.CurrentPerformanceStats;
            car.Controller.ApplyPerformanceStats(baseline);
            car.Input.CurrentInput = new DrivingInput(0.3f, 0f, 0.4f, false, false);
            car.Controller.SendMessage("FixedUpdate");
            var torque = car.Controller.CurrentMotorTorque;
            var steer = car.Controller.CurrentFrontWheelSteerAngle;
            var rear = Wheel(car, "rearRightWheel");
            var grip = rear.sidewaysFriction.stiffness;
            var garage = CreateGarage(baseline);
            Assert.That(garage.Purchase(UpgradeType.Engine).Succeeded, Is.True);
            Assert.That(garage.Purchase(UpgradeType.Handling).Succeeded, Is.True);
            var upgraded = garage.CalculateEffectiveStats();

            car.Controller.ApplyPerformanceStats(upgraded);
            car.Controller.SendMessage("FixedUpdate");
            Assert.That(car.Controller.CurrentMotorTorque, Is.GreaterThan(torque * 1.07f));
            Assert.That(car.Controller.CurrentFrontWheelSteerAngle, Is.GreaterThan(steer * 1.07f));
            Assert.That(rear.sidewaysFriction.stiffness, Is.GreaterThan(grip));
            var upgradedGrip = rear.sidewaysFriction.stiffness;
            car.Controller.ApplyPerformanceStats(upgraded);
            car.Controller.SendMessage("FixedUpdate");
            Assert.That(rear.sidewaysFriction.stiffness, Is.EqualTo(upgradedGrip));

            car.Input.CurrentInput = new DrivingInput(0.3f, 0f, 0f, true, false);
            car.Controller.SendMessage("FixedUpdate");
            var configuration = AssetDatabase.LoadAssetAtPath<WheelVehicleConfiguration>(
                "Assets/ApexRivals/Vehicle/Configuration/VanguardWheelVehicleConfiguration.asset");
            Assert.That(rear.sidewaysFriction.stiffness, Is.EqualTo(configuration.rearSidewaysFriction.stiffness
                * configuration.driftRearSidewaysGripMultiplier * upgraded.DriftHandling / baseline.DriftHandling).Within(0.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EngineUpgradeIncreasesMeasuredAccelerationWithTheSameInput()
        {
            var baseline = CreateCar(-10f);
            var upgraded = CreateCar(10f);
            Settle(baseline, upgraded);
            var garage = CreateGarage(baseline.Controller.CurrentPerformanceStats);
            for (var i = 0; i < 3; i++) Assert.That(garage.Purchase(UpgradeType.Engine).Succeeded, Is.True);
            baseline.Controller.ApplyPerformanceStats(baseline.Controller.CurrentPerformanceStats);
            upgraded.Controller.ApplyPerformanceStats(garage.CalculateEffectiveStats());
            baseline.Input.CurrentInput = new DrivingInput(0.3f, 0f, 0f, false, false);
            upgraded.Input.CurrentInput = baseline.Input.CurrentInput;

            for (var i = 0; i < 200; i++) Simulate(baseline, upgraded);

            var baseSpeed = Vector3.Dot(baseline.Body.linearVelocity, Vector3.forward);
            var upgradedSpeed = Vector3.Dot(upgraded.Body.linearVelocity, Vector3.forward);
            Assert.That(baseSpeed, Is.GreaterThan(0.5f));
            Assert.That(upgradedSpeed, Is.GreaterThan(baseSpeed * 1.05f));
            Debug.Log($"PHASE_TWO_ACCELERATION base={baseSpeed:F3} upgraded={upgradedSpeed:F3} m/s after 4 simulated seconds at throttle=0.3");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UpgradedSpeedLimitRetainsTorqueAboveBaseLimitAndDoesNotClampVelocity()
        {
            var car = CreateCar(0f);
            var stats = car.Controller.CurrentPerformanceStats;
            car.Controller.ApplyPerformanceStats(stats);
            car.Input.CurrentInput = new DrivingInput(1f, 0f, 0f, false, false);
            car.Body.linearVelocity = Vector3.forward * stats.TopSpeed;
            car.Controller.SendMessage("FixedUpdate");
            Assert.That(car.Controller.CurrentMotorTorque, Is.Zero);
            var garage = CreateGarage(stats);
            garage.Purchase(UpgradeType.Engine);
            car.Controller.ApplyPerformanceStats(garage.CalculateEffectiveStats());
            car.Controller.SendMessage("FixedUpdate");
            Assert.That(car.Controller.CurrentMotorTorque, Is.GreaterThan(0f));
            car.Body.linearVelocity = Vector3.forward * 100f;
            car.Controller.SendMessage("FixedUpdate");
            Assert.That(car.Controller.CurrentMotorTorque, Is.Zero);
            Assert.That(car.Body.linearVelocity.z, Is.EqualTo(100f));
            car.Body.linearVelocity = Vector3.back * 2f;
            car.Input.CurrentInput = new DrivingInput(0f, 1f, 0f, false, false);
            car.Controller.SendMessage("FixedUpdate");
            Assert.That(car.Controller.CurrentMotorTorque, Is.LessThan(0f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShiftAndCooldownFinishWhileThrottleIsReleased()
        {
            var car = CreateCar(0f);
            Set(car.Controller, "_shiftTimeRemaining", 0.04f);
            car.Input.CurrentInput = DrivingInput.Neutral;
            for (var i = 0; i < 50; i++) car.Controller.SendMessage("FixedUpdate");
            Assert.That(car.Controller.ShiftInProgress, Is.False);
            Assert.That(car.Controller.ShiftCooldownRemaining, Is.Zero);
            car.Input.CurrentInput = new DrivingInput(0.3f, 0f, 0f, false, false);
            car.Controller.SendMessage("FixedUpdate");
            Assert.That(car.Controller.CurrentMotorTorque, Is.GreaterThan(0f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecoveryAtAcceptedCheckpointPreservesProgressAndClearsForces()
        {
            var car = CreateCar(0f);
            var coordinator = CreateRace(car, out var checkpoint);
            var participant = car.Root.GetComponent<RaceParticipant>();
            Settle(car);
            car.Body.position = new Vector3(0f, 1f, 15f);
            Assert.That(coordinator.TryPassCheckpoint(participant, checkpoint).Accepted, Is.True);
            Settle(car);
            Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
            var next = progress.NextExpectedCheckpointIndex;
            var passed = progress.TotalPassedCheckpoints;
            var completedLaps = progress.CompletedLaps;
            var recovery = car.Root.GetComponent<PlayerCheckpointRecovery>();
            recovery.SendMessage("FixedUpdate");
            var anchor = Get<Vector3>(car.Resetter, "_latestValidPosition");
            Assert.That(Vector3.Distance(anchor, new Vector3(0f, anchor.y, 15f)), Is.LessThan(2f));

            car.Body.position = new Vector3(20f, 2f, 15f);
            car.Body.rotation = Quaternion.Euler(0f, 0f, 180f);
            Set(car.Controller, "_currentGearIndex", 3);
            Set(car.Controller, "_shiftTimeRemaining", 0.2f);
            Wheel(car, "rearRightWheel").brakeTorque = 1000f;
            Assert.That(car.Controller.TryResetVehicle(), Is.True);
            Assert.That(Vector3.Distance(car.Body.position, anchor), Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(car.Root.transform.up, Vector3.up), Is.GreaterThan(0.99f));
            Assert.That(car.Controller.CurrentGear, Is.EqualTo(1));
            Assert.That(car.Controller.ShiftInProgress, Is.False);
            Assert.That(car.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(car.Body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(Wheel(car, "rearRightWheel").brakeTorque, Is.Zero);
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(next));
            Assert.That(progress.TotalPassedCheckpoints, Is.EqualTo(passed));
            Assert.That(progress.CompletedLaps, Is.EqualTo(completedLaps));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BlockedRecoveryPoseIsRejectedUntilClear()
        {
            var car = CreateCar(0f);
            var anchor = new Vector3(0f, 0.5f, 15f);
            car.Resetter.CaptureResetPose(anchor, Quaternion.identity);
            var obstruction = CreatePrimitive("OccupiedAnchor", anchor + Vector3.up, new Vector3(4f, 3f, 6f));
            _physics.Simulate(Step);
            var before = car.Body.position;
            Assert.That(car.Controller.TryResetVehicle(), Is.False);
            Assert.That(car.Body.position, Is.EqualTo(before));
            Object.DestroyImmediate(obstruction);
            _physics.Simulate(Step);
            Assert.That(car.Controller.TryResetVehicle(), Is.True);
            Assert.That(Vector3.Distance(car.Body.position, anchor), Is.LessThan(0.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvertedRoofRecoveryRequestUsesTheSafeAnchorAndIsConsumedOnce()
        {
            var car = CreateCar(0f);
            CreateRace(car, out _);
            var anchor = new Vector3(0f, 0.5f, 15f);
            car.Resetter.CaptureResetPose(anchor, Quaternion.identity);
            car.Body.position = new Vector3(20f, 2f, 0f);
            car.Body.rotation = Quaternion.Euler(0f, 0f, 180f);
            Settle(car);
            var detector = car.Root.GetComponent<VehicleRoofRecoveryDetector>();
            Set(detector, "invertedDebounceDuration", 0f);
            detector.SendMessage("Update");
            Assert.That(detector.IsInverted, Is.True);
            Assert.That(detector.RoofRayHit, Is.True);
            Assert.That(detector.CanRecoverVehicle, Is.True);
            var completed = 0;
            detector.RecoveryCompleted += _ => completed++;
            car.Input.CurrentInput = new DrivingInput(0f, 0f, 0f, false, false, RecoveryInputSource.KeyboardE);

            detector.SendMessage("Update");
            detector.SendMessage("Update");

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(car.Input.CurrentInput.RecoverVehicleRequested, Is.False);
            Assert.That(Vector3.Distance(car.Body.position, anchor), Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(car.Root.transform.up, Vector3.up), Is.GreaterThan(0.99f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkippedCheckpointAndAirborneCrossingCannotOverwriteTheSafeAnchor()
        {
            var car = CreateCar(0f);
            var coordinator = CreateRace(car, out var checkpoint);
            var participant = car.Root.GetComponent<RaceParticipant>();
            Settle(car);
            var initialAnchor = Get<Vector3>(car.Resetter, "_latestValidPosition");
            Assert.That(coordinator.TryPassCheckpoint(participant, coordinator.OrderedCheckpoints[2]).Accepted, Is.False);
            Assert.That(Get<Vector3>(car.Resetter, "_latestValidPosition"), Is.EqualTo(initialAnchor));
            car.Body.position = new Vector3(0f, 10f, 15f);

            Assert.That(coordinator.TryPassCheckpoint(participant, checkpoint).Accepted, Is.True);
            Simulate(car);
            Assert.That(Get<Vector3>(car.Resetter, "_latestValidPosition"), Is.EqualTo(initialAnchor));
            Settle(car);
            var groundedAnchor = Get<Vector3>(car.Resetter, "_latestValidPosition");
            Assert.That(groundedAnchor.z, Is.EqualTo(15f).Within(1f));
            Assert.That(groundedAnchor.y, Is.LessThan(2f));
            yield return null;
        }

        private Car CreateCar(float x)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ApexRivals/Vehicle/Prefabs/Vehicle_Vanguard.prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab, new Vector3(x, 1f, 0f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(root, _scene);
            _objects.Add(root);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var car = new Car { Root = root, Body = root.GetComponent<Rigidbody>(), Controller = root.GetComponent<WheelArcadeVehicleController>(), Resetter = root.GetComponent<VehicleResetter>() };
            car.Body.interpolation = RigidbodyInterpolation.None;
            car.Controller.ConfigureRuntime(car.Input, car.Resetter);
            return car;
        }

        private GarageService CreateGarage(VehiclePerformanceStats baseline)
        {
            var engine = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/EngineUpgradeDefinition.asset");
            var handling = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>("Assets/ApexRivals/Content/Configuration/Garage/HandlingUpgradeDefinition.asset");
            Assert.That(engine, Is.Not.Null);
            Assert.That(handling, Is.Not.Null);
            return new GarageService(new PlayerProgressionState(10000), engine.CreateData(), handling.CreateData(), baseline);
        }

        private RaceCoordinator CreateRace(Car car, out RaceCheckpoint next)
        {
            var owner = new GameObject("RaceCoordinator");
            SceneManager.MoveGameObjectToScene(owner, _scene);
            _objects.Add(owner);
            owner.SetActive(false);
            var coordinator = owner.AddComponent<RaceCoordinator>();
            Set(coordinator, "startCountdownOnEnable", false);
            var definition = ScriptableObject.CreateInstance<RaceDefinition>();
            Set(definition, "countdownDuration", 0f);
            Set(coordinator, "raceDefinition", definition);
            var checkpoints = new RaceCheckpoint[3];
            for (var i = 0; i < 3; i++)
            {
                var cp = new GameObject("CP" + i);
                SceneManager.MoveGameObjectToScene(cp, _scene);
                _objects.Add(cp);
                cp.transform.position = new Vector3(0f, 1.5f, i * 15f);
                var trigger = cp.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(8f, 4f, 3f);
                checkpoints[i] = cp.AddComponent<RaceCheckpoint>();
                Set(checkpoints[i], "raceCoordinator", coordinator);
                Set(checkpoints[i], "checkpointIndex", i);
            }

            Set(coordinator, "orderedCheckpoints", checkpoints);
            owner.SetActive(true);
            coordinator.enabled = false;
            var composition = car.Root.GetComponent<RaceVehicleComposition>();
            composition.ConfigurePlayer(coordinator, "Player", car.Controller.CurrentPerformanceStats);
            coordinator.ConfigureSession(new[] { composition.RaceParticipant }, new[] { composition.DrivingInputGate }, composition.RaceParticipant);
            composition.DrivingInputGate.Configure(car.Input, false);
            coordinator.StartCountdown();
            coordinator.SendMessage("UpdateCountdown");
            Assert.That(coordinator.State, Is.EqualTo(RaceState.Racing));
            next = checkpoints[1];
            Object.DestroyImmediate(definition);
            return coordinator;
        }

        private void Settle(params Car[] cars)
        {
            foreach (var car in cars) car.Input.CurrentInput = DrivingInput.Neutral;
            for (var i = 0; i < 100; i++) Simulate(cars);
        }

        private void Simulate(params Car[] cars)
        {
            foreach (var car in cars) car.Controller.SendMessage("FixedUpdate");
            _physics.Simulate(Step);
            foreach (var car in cars)
            {
                var recovery = car.Root.GetComponent<PlayerCheckpointRecovery>();
                if (recovery != null) recovery.SendMessage("FixedUpdate");
            }
        }

        private GameObject CreatePrimitive(string name, Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            SceneManager.MoveGameObjectToScene(obj, _scene);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            _objects.Add(obj);
            return obj;
        }

        private static WheelCollider Wheel(Car car, string field) => Get<WheelCollider>(car.Controller, field);
        private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }
}
#endif
