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
            public VehicleChassisStability Stability;
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

        [UnityTest]
        public IEnumerator AcceptedAirborneCheckpointWithBlockedRoadNeverFallsBackToStart()
        {
            var car = CreateCar(0);
            var coordinator = CreateRace(car, out var checkpoint);
            coordinator.ConfigurePositionRoute(new[] { Vector3.zero, new Vector3(0,0,15), new Vector3(0,0,30),
                new Vector3(20,0,30), new Vector3(20,0,0) });
            Assert.That(coordinator.HasRecoveryRoute, Is.True);
            var participant = car.Root.GetComponent<RaceParticipant>();
            car.Body.position = new Vector3(0,10,15);
            Assert.That(coordinator.TryPassCheckpoint(participant, checkpoint).Accepted, Is.True);
            var obstruction = CreatePrimitive("OccupiedCheckpointLanes", new Vector3(0,1.5f,15), new Vector3(10,3,8));
            Physics.SyncTransforms();
            car.Body.position = new Vector3(40,5,15);
            car.Body.rotation = Quaternion.Euler(0,0,180);
            var before = car.Body.position;
            Assert.That(car.Controller.TryResetVehicle(), Is.False);
            Assert.That(car.Body.position, Is.EqualTo(before), "An occupied recent checkpoint must not send the car to an obsolete starting pose.");
            Object.DestroyImmediate(obstruction);
            Physics.SyncTransforms();
            Assert.That(car.Controller.TryResetVehicle(), Is.True);
            Assert.That(car.Body.position.z, Is.EqualTo(15).Within(.1f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoGroundedWheelsAdvanceRecoveryBetweenCheckpoints()
        {
            var car = CreateCar(0);
            var coordinator = CreateRace(car, out var checkpoint);
            coordinator.ConfigurePositionRoute(new[] { Vector3.zero, new Vector3(0,0,15), new Vector3(0,0,30),
                new Vector3(20,0,30), new Vector3(20,0,0) });
            car.Body.constraints = RigidbodyConstraints.FreezeRotation;
            Wheel(car, "frontLeftWheel").enabled = false;
            Wheel(car, "rearRightWheel").enabled = false;
            car.Body.position = new Vector3(0,1,15);
            var participant = car.Root.GetComponent<RaceParticipant>();
            Assert.That(coordinator.TryPassCheckpoint(participant, checkpoint).Accepted, Is.True);
            car.Body.position = new Vector3(0,1,23);
            Settle(car);
            Assert.That(Wheel(car, "frontRightWheel").isGrounded, Is.True);
            Assert.That(Wheel(car, "rearLeftWheel").isGrounded, Is.True);
            car.Body.constraints = RigidbodyConstraints.None;
            car.Body.position = new Vector3(30,5,23);
            car.Body.rotation = Quaternion.Euler(0,0,180);
            Assert.That(car.Controller.TryResetVehicle(), Is.True);
            Assert.That(car.Body.position.z, Is.EqualTo(23).Within(1));
            Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stability_AllEightCarsStayUprightInHardTurnsAndDrifts()
        {
            var failures = new List<string>();
            foreach (var model in new[] { "Vanguard", "Striker", "Sunaru", "Borshe911", "Aduo", "Fernedenz", "Massalini", "Fehhahi" })
            {
                foreach (var maneuver in new[] { "turn", "drift", "sideways-slide", "kerb-slide" })
                {
                    var car = CreateCar(0, model);
                    Settle(car);
                    car.Input.CurrentInput = new DrivingInput(.75f, 0, 0, false, false);
                    for (var step = 0; step < 200; step++) Simulate(car);
                    GameObject kerb = null;
                    if (maneuver == "kerb-slide")
                    {
                        kerb = CreatePrimitive("SlideKerb", new Vector3(2.3f,.06f,300), new Vector3(.8f,.12f,2000));
                        Physics.SyncTransforms();
                    }
                    if (maneuver == "sideways-slide" || maneuver == "kerb-slide")
                        car.Body.linearVelocity = car.Root.transform.forward * 20 + car.Root.transform.right * 12;
                    var minimumUp = 1f;
                    var maximumVerticalSpeed = 0f;
                    car.Input.CurrentInput = new DrivingInput(.7f, 0, maneuver == "sideways-slide" ? .3f : .8f, maneuver != "turn", false);
                    for (var step = 0; step < 300; step++)
                    {
                        Simulate(car);
                        minimumUp = Mathf.Min(minimumUp, Vector3.Dot(car.Body.rotation * Vector3.up, Vector3.up));
                        maximumVerticalSpeed = Mathf.Max(maximumVerticalSpeed, Mathf.Abs(car.Body.linearVelocity.y));
                    }
                    var tilt = Mathf.Acos(Mathf.Clamp(minimumUp, -1, 1)) * Mathf.Rad2Deg;
                    Debug.Log($"STABILITY {model} {maneuver}: peakTilt={tilt:F2}deg maxVerticalSpeed={maximumVerticalSpeed:F2}m/s");
                    if (minimumUp < .5f) failures.Add(model + " " + maneuver + ": tilt=" + tilt.ToString("F2"));
                    Object.DestroyImmediate(car.Root);
                    if (kerb != null) Object.DestroyImmediate(kerb);
                }
            }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stability_AllEightCarsReduceRollFromTheSameDisturbance()
        {
            foreach (var model in new[] { "Vanguard", "Striker", "Sunaru", "Borshe911", "Aduo", "Fernedenz", "Massalini", "Fehhahi" })
            {
                var peak = new float[2];
                for (var variant = 0; variant < 2; variant++)
                {
                    var car = CreateCar(0, model);
                    Assert.That(car.Stability, Is.Not.Null, model);
                    if (variant == 0) car.Stability = null; // Automatic centre of mass and no roll support, as before.
                    Settle(car);
                    car.Body.linearVelocity = Vector3.forward * 20;
                    car.Body.angularVelocity = Vector3.forward * 4;
                    for (var step = 0; step < 150; step++)
                    {
                        Simulate(car);
                        var tilt = Mathf.Acos(Mathf.Clamp(Vector3.Dot(car.Body.rotation * Vector3.up, Vector3.up), -1, 1)) * Mathf.Rad2Deg;
                        peak[variant] = Mathf.Max(peak[variant], tilt);
                    }
                    Object.DestroyImmediate(car.Root);
                }
                Debug.Log($"ROLL_COMPARISON {model}: automatic={peak[0]:F2}deg stabilized={peak[1]:F2}deg");
                Assert.That(peak[1], Is.LessThan(peak[0] * .9f), model + " must measurably reduce the same roll disturbance.");
                Assert.That(peak[1], Is.LessThan(60), model + " must remain upright.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stability_CorrectsGroundedRollButDoesNotSelfRightOrSteerInAir()
        {
            var car = CreateCar(0);
            Assert.That(car.Stability, Is.Not.Null);
            Settle(car);
            Assert.That(car.Body.centerOfMass.y, Is.EqualTo(.22f).Within(.001f));
            car.Body.angularVelocity = Vector3.forward * 2;
            car.Stability.SendMessage("FixedUpdate");
            Assert.That(car.Stability.SupportedWheelCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(car.Stability.CurrentRollAcceleration, Is.LessThan(0));
            car.Body.position += Vector3.up * 20;
            car.Body.angularVelocity = new Vector3(1,2,3);
            car.Stability.SendMessage("FixedUpdate");
            Assert.That(car.Stability.SupportedWheelCount, Is.Zero);
            Assert.That(car.Stability.CurrentRollAcceleration, Is.Zero);
            Assert.That(car.Body.angularVelocity, Is.EqualTo(new Vector3(1,2,3)));
            car.Body.rotation = Quaternion.Euler(0,0,180);
            car.Stability.SendMessage("FixedUpdate");
            Assert.That(car.Stability.CurrentRollAcceleration, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Steering_AllProductionCarsCanAccelerateFromRestWhileTurning()
        {
            var models = new[] { "Vanguard", "Striker", "Sunaru", "Borshe911", "Aduo", "Fehhahi", "Fernedenz", "Massalini" };
            for (var model = 0; model < models.Length; model++)
            {
                var straight = CreateCar(-350f + model * 90f, models[model]);
                var turning = CreateCar(-320f + model * 90f, models[model]);
                Settle(straight, turning);
                straight.Input.CurrentInput = new DrivingInput(1f, 0f, 0f, false, false);
                turning.Input.CurrentInput = new DrivingInput(1f, 0f, 1f, false, false);
                var straightDistance = 0f;
                var turningDistance = 0f;
                var maximumHeading = 0f;
                for (var step = 0; step < 150; step++)
                {
                    Simulate(straight, turning);
                    straightDistance += Vector3.ProjectOnPlane(straight.Body.linearVelocity, Vector3.up).magnitude * Step;
                    turningDistance += Vector3.ProjectOnPlane(turning.Body.linearVelocity, Vector3.up).magnitude * Step;
                    maximumHeading = Mathf.Max(maximumHeading, Vector3.Angle(Vector3.forward, turning.Root.transform.forward));
                    if (step == 0)
                        Assert.That(Mathf.Abs(turning.Controller.CurrentFrontWheelSteerAngle), Is.LessThanOrEqualTo(28f), models[model]);
                }

                Debug.Log($"STEERED_LAUNCH {models[model]} straight={straightDistance:F2}m turning={turningDistance:F2}m speed={turning.Controller.CurrentPlanarSpeed:F2}m/s");
                Assert.That(turning.Controller.CurrentPlanarSpeed, Is.GreaterThan(2.5f), models[model] + " must gain speed while the player holds steering.");
                Assert.That(turningDistance, Is.GreaterThan(straightDistance * 0.35f), models[model] + " must not stall against its front tyres.");
                Assert.That(maximumHeading, Is.GreaterThan(15f), models[model] + " must still turn while accelerating.");
                Object.DestroyImmediate(straight.Root);
                Object.DestroyImmediate(turning.Root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Steering_LockedRearWheelsDoNotRestoreFullLockAtHighRoadSpeed()
        {
            var car = CreateCar(0);
            Settle(car);
            car.Body.linearVelocity = Vector3.forward * 25;
            Wheel(car, "rearLeftWheel").brakeTorque = 50000;
            Wheel(car, "rearRightWheel").brakeTorque = 50000;
            _physics.Simulate(Step);
            Assert.That(Mathf.Abs(Wheel(car, "rearRightWheel").rpm), Is.LessThan(5), "Reproduce the locked-wheel phase of a handbrake drift.");
            Assert.That(car.Body.linearVelocity.magnitude, Is.GreaterThan(20));
            car.Input.CurrentInput = new DrivingInput(.7f, 0, 1, true, false);
            car.Controller.SendMessage("FixedUpdate");
            Debug.Log($"LOCKED_WHEEL_STEERING roadSpeed={car.Controller.CurrentPlanarSpeed:F2}m/s rearRpm={car.Controller.RearRightWheelRpm:F2} angle={car.Controller.CurrentFrontWheelSteerAngle:F2}deg");
            Assert.That(Mathf.Abs(car.Controller.CurrentFrontWheelSteerAngle), Is.LessThan(25), "Locking rear tyres at speed must not enable parking-speed steering angles.");
            yield return null;
        }

        private Car CreateCar(float x, string model = "Vanguard")
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ApexRivals/Vehicle/Prefabs/Vehicle_" + model + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab, new Vector3(x, 1f, 0f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(root, _scene);
            _objects.Add(root);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var car = new Car { Root = root, Body = root.GetComponent<Rigidbody>(), Controller = root.GetComponent<WheelArcadeVehicleController>(), Resetter = root.GetComponent<VehicleResetter>() };
            car.Stability = root.GetComponent<VehicleChassisStability>();
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
            foreach (var car in cars)
            {
                car.Controller.SendMessage("FixedUpdate");
                if (car.Stability != null) car.Stability.SendMessage("FixedUpdate");
            }
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
