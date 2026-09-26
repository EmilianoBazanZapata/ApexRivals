using System.Collections.Generic;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.RaceSetup
{
    // Covers the HUD's read-only telemetry boundary: RaceVehicleComposition exposing
    // IVehicleTelemetry from its controller, and RaceCoordinatorSessionAdapter
    // resolving the PLAYER's telemetry specifically (never an AI/opponent's), the same
    // production path RaceUiSceneInstaller uses to bind the Race HUD.
    public sealed class RaceVehicleTelemetryTests
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
        public void VehicleTelemetry_ResolvesFromControllerImplementingTheInterface()
        {
            var composition = CreateComposition("Vehicle", out var telemetry);
            telemetry.SpeedKph = 142f;
            telemetry.CurrentGear = 4;
            telemetry.EngineRpm = 6350f;
            telemetry.IsReversing = false;

            var resolved = composition.VehicleTelemetry;

            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved.SpeedKph, Is.EqualTo(142f));
            Assert.That(resolved.CurrentGear, Is.EqualTo(4));
            Assert.That(resolved.EngineRpm, Is.EqualTo(6350f));
            Assert.That(resolved.IsReversing, Is.False);
        }

        [Test]
        public void VehicleTelemetry_IsNullWhenControllerDoesNotImplementIt()
        {
            var root = new GameObject("NoTelemetryVehicle");
            _createdObjects.Add(root);
            var plainController = root.AddComponent<NonTelemetryController>();
            var participant = root.AddComponent<RaceParticipant>();
            var gate = root.AddComponent<DrivingInputGate>();
            var composition = root.AddComponent<RaceVehicleComposition>();
            composition.ConfigureReferences(plainController, null, participant, gate, null, null, root.transform, root.transform, root.transform, null);

            Assert.That(composition.VehicleTelemetry, Is.Null);
        }

        [Test]
        public void PlayerVehicleTelemetry_ResolvesPlayerAndIgnoresOpponents()
        {
            var playerComposition = CreateComposition("PlayerVehicle", out var playerTelemetry);
            playerTelemetry.SpeedKph = 80f;
            playerTelemetry.CurrentGear = 3;
            playerTelemetry.EngineRpm = 4200f;

            var opponentComposition = CreateComposition("OpponentVehicle", out var opponentTelemetry);
            opponentTelemetry.SpeedKph = 999f;
            opponentTelemetry.CurrentGear = 9;
            opponentTelemetry.EngineRpm = 9999f;

            var coordinatorObject = new GameObject("RaceCoordinator");
            _createdObjects.Add(coordinatorObject);
            var raceCoordinator = coordinatorObject.AddComponent<RaceCoordinator>();
            var adapter = new RaceCoordinatorSessionAdapter(raceCoordinator);

            var setupOutput = new RaceSetupOutput(
                playerComposition,
                new[] { opponentComposition },
                new[] { playerComposition.RaceParticipant, opponentComposition.RaceParticipant },
                playerComposition.transform,
                new[] { playerComposition.DrivingInputGate, opponentComposition.DrivingInputGate });

            var configured = adapter.ConfigureForSession(setupOutput, "Player", out var message);

            Assert.That(configured, Is.True, message);
            var resolved = adapter.PlayerVehicleTelemetry;
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved.SpeedKph, Is.EqualTo(80f));
            Assert.That(resolved.CurrentGear, Is.EqualTo(3));
            Assert.That(resolved.EngineRpm, Is.EqualTo(4200f));
            Assert.That(resolved, Is.Not.SameAs(opponentTelemetry));
        }

        private RaceVehicleComposition CreateComposition(string name, out FakeVehicleTelemetryController telemetry)
        {
            var root = new GameObject(name);
            _createdObjects.Add(root);
            telemetry = root.AddComponent<FakeVehicleTelemetryController>();
            var participant = root.AddComponent<RaceParticipant>();
            var gate = root.AddComponent<DrivingInputGate>();
            var composition = root.AddComponent<RaceVehicleComposition>();
            composition.ConfigureReferences(telemetry, null, participant, gate, null, null, root.transform, root.transform, root.transform, null);
            return composition;
        }

        private sealed class FakeVehicleTelemetryController : MonoBehaviour, IVehicleTelemetry
        {
            public float SpeedKph { get; set; }
            public int CurrentGear { get; set; }
            public float EngineRpm { get; set; }
            public bool IsReversing { get; set; }
        }

        private sealed class NonTelemetryController : MonoBehaviour
        {
        }
    }
}
