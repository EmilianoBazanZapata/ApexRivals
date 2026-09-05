using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.AI.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Input.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.RaceSetup
{
    public sealed class RaceSetupServiceTests
    {
        [Test]
        public void SetupRace_Success_ReturnsRequiredReferences()
        {
            var context = CreateContext();

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Output.Player, Is.Not.Null);
            Assert.That(result.Output.Opponents, Has.Count.EqualTo(1));
            Assert.That(result.Output.Participants, Has.Count.EqualTo(2));
            Assert.That(result.Output.DrivingGates, Has.Count.EqualTo(2));
            Assert.That(result.Output.PlayerCameraTarget, Is.Not.Null);
        }

        [Test]
        public void SetupRace_CannotRunTwice()
        {
            var context = CreateContext();
            context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Status, Is.EqualTo(RaceSetupStatus.AlreadySetup));
        }

        [Test]
        public void SetupRace_PartialSpawnFailure_CleansCreatedInstances()
        {
            var context = CreateContext();
            context.Factory.FailOnCreateNumber = 2;

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Status, Is.EqualTo(RaceSetupStatus.SpawnFailed));
            Assert.That(context.Factory.DestroyedCount, Is.EqualTo(1));
            Assert.That(context.RaceCoordinator.State, Is.EqualTo(RaceState.NotStarted));
        }

        [Test]
        public void SetupRace_Failure_DoesNotStartRace()
        {
            var context = CreateContext();
            var invalidRoster = new RaceRoster(new[] { new RaceRosterEntry("Player", RaceEntryType.Player, "missing", 0, string.Empty) });

            var result = context.Service.SetupRace(invalidRoster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(context.RaceCoordinator.State, Is.EqualTo(RaceState.NotStarted));
        }

        [Test]
        public void SetupRace_PlayerUpgradesAreAppliedOnce()
        {
            var context = CreateContext(engineLevel: 1, handlingLevel: 1);

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            var stats = result.Output.Player.VehicleController.EffectiveStats;
            Assert.That(stats.ForwardAcceleration, Is.EqualTo(15f));
            Assert.That(stats.SteeringStrength, Is.EqualTo(42f));
        }

        [Test]
        public void SetupRace_RecompositionDoesNotAccumulateUpgrades()
        {
            var first = CreateContext(engineLevel: 1, handlingLevel: 1);
            var second = CreateContext(engineLevel: 1, handlingLevel: 1);

            var firstResult = first.Service.SetupRace(first.Roster, first.StartingGrid, first.RaceCoordinator, first.RacingLine);
            var secondResult = second.Service.SetupRace(second.Roster, second.StartingGrid, second.RaceCoordinator, second.RacingLine);

            Assert.That(secondResult.Output.Player.VehicleController.EffectiveStats.ForwardAcceleration, Is.EqualTo(firstResult.Output.Player.VehicleController.EffectiveStats.ForwardAcceleration));
            Assert.That(secondResult.Output.Player.VehicleController.EffectiveStats.SteeringStrength, Is.EqualTo(firstResult.Output.Player.VehicleController.EffectiveStats.SteeringStrength));
        }

        [Test]
        public void SetupRace_AiDoesNotReceivePlayerUpgradesByDefault()
        {
            var context = CreateContext(engineLevel: 1, handlingLevel: 1);

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            var aiStats = result.Output.Opponents[0].VehicleController.EffectiveStats;
            Assert.That(aiStats.ForwardAcceleration, Is.EqualTo(10f));
            Assert.That(aiStats.SteeringStrength, Is.EqualTo(30f));
        }

        [Test]
        public void SetupRace_DoesNotAffectSceneFlowNavigation()
        {
            var context = CreateContext();
            var sceneFlow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), new FakeSceneLoader());

            context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(sceneFlow.State, Is.EqualTo(ApplicationState.Bootstrapping));
        }

        private static TestContext CreateContext(int engineLevel = 0, int handlingLevel = 0)
        {
            var progression = new PlayerProgressionState();
            progression.SetUpgradeLevel(UpgradeType.Engine, engineLevel);
            progression.SetUpgradeLevel(UpgradeType.Handling, handlingLevel);

            var vehicleConfiguration = ScriptableObject.CreateInstance<ArcadeVehicleConfiguration>();
            vehicleConfiguration.forwardAcceleration = 10f;
            vehicleConfiguration.maximumForwardSpeed = 20f;
            vehicleConfiguration.steeringStrength = 30f;

            var prefab = new GameObject("VehiclePrefab");
            var catalog = new VehicleCatalog(new[]
            {
                new VehicleDefinitionData("starter", "Starter", string.Empty, vehicleConfiguration, prefab, null, true)
            }, "starter");

            var factory = new FakeRaceVehicleFactory();
            var service = new RaceSetupService(
                catalog,
                progression,
                CreateEngineDefinition(),
                CreateHandlingDefinition(),
                factory,
                new Dictionary<string, AiDriverConfiguration> { { "standard", ScriptableObject.CreateInstance<AiDriverConfiguration>() } });

            var startingGridObject = new GameObject("StartingGrid");
            var startingGrid = startingGridObject.AddComponent<StartingGrid>();
            var spawn0 = new GameObject("Spawn0").transform;
            var spawn1 = new GameObject("Spawn1").transform;
            startingGrid.Configure(new[] { spawn0, spawn1 });

            var raceCoordinator = new GameObject("RaceCoordinator").AddComponent<RaceCoordinator>();
            var racingLine = new GameObject("RacingLine").AddComponent<RacingLine>();
            var roster = new RaceRoster(new[]
            {
                new RaceRosterEntry("Player", RaceEntryType.Player, "starter", 0, string.Empty),
                new RaceRosterEntry("AI_01", RaceEntryType.AI, "starter", 1, "standard")
            });

            return new TestContext(service, factory, roster, startingGrid, raceCoordinator, racingLine);
        }

        private static UpgradeDefinitionData CreateEngineDefinition()
        {
            return new UpgradeDefinitionData(
                UpgradeType.Engine,
                new[] { new UpgradeLevel(100, new UpgradeStatModifier(1.5f, 1.2f, 1f, 0f, 0f)) });
        }

        private static UpgradeDefinitionData CreateHandlingDefinition()
        {
            return new UpgradeDefinitionData(
                UpgradeType.Handling,
                new[] { new UpgradeLevel(100, new UpgradeStatModifier(1f, 1f, 1.4f, 0.05f, 0f)) });
        }

        private readonly struct TestContext
        {
            public TestContext(
                RaceSetupService service,
                FakeRaceVehicleFactory factory,
                RaceRoster roster,
                StartingGrid startingGrid,
                RaceCoordinator raceCoordinator,
                RacingLine racingLine)
            {
                Service = service;
                Factory = factory;
                Roster = roster;
                StartingGrid = startingGrid;
                RaceCoordinator = raceCoordinator;
                RacingLine = racingLine;
            }

            public RaceSetupService Service { get; }
            public FakeRaceVehicleFactory Factory { get; }
            public RaceRoster Roster { get; }
            public StartingGrid StartingGrid { get; }
            public RaceCoordinator RaceCoordinator { get; }
            public RacingLine RacingLine { get; }
        }

        private sealed class FakeRaceVehicleFactory : IRaceVehicleFactory
        {
            private int _createCount;

            public int FailOnCreateNumber { get; set; }
            public int DestroyedCount { get; private set; }

            public bool TryCreate(GameObject prefab, SpawnPose pose, out RaceVehicleComposition composition, out string message)
            {
                _createCount++;
                composition = null;
                if (FailOnCreateNumber == _createCount)
                {
                    message = "Spawn failed.";
                    return false;
                }

                composition = CreateComposition();
                message = string.Empty;
                return true;
            }

            public void Destroy(RaceVehicleComposition composition)
            {
                DestroyedCount++;
                if (composition != null)
                {
                    Object.DestroyImmediate(composition.gameObject);
                }
            }

            private static RaceVehicleComposition CreateComposition()
            {
                var root = new GameObject("RaceVehicle");
                var controller = root.AddComponent<ArcadeVehicleController>();
                var resetter = root.AddComponent<VehicleResetter>();
                var participant = root.AddComponent<RaceParticipant>();
                var gate = root.AddComponent<DrivingInputGate>();
                var playerInput = root.AddComponent<FakeInputProvider>();
                var aiInput = root.AddComponent<AiDrivingInputProvider>();
                var composition = root.AddComponent<RaceVehicleComposition>();
                composition.ConfigureReferences(controller, resetter, participant, gate, playerInput, aiInput, root.transform, root.transform, root.transform, null);
                return composition;
            }
        }

        private sealed class FakeInputProvider : MonoBehaviour, IDrivingInputProvider
        {
            public DrivingInput CurrentInput => DrivingInput.Neutral;

            public void ConsumeResetRequest()
            {
            }
        }

        private sealed class FakeSceneLoader : IContentSceneLoader
        {
            public float Progress => 0f;
            public bool IsLoading => false;
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName => string.Empty;

            public System.Threading.Tasks.Task<ContentSceneLoadResult> LoadContentSceneAsync(
                ContentSceneId sceneId,
                string sceneName,
                bool reloadCurrentScene,
                System.Threading.CancellationToken cancellationToken = default)
            {
                return System.Threading.Tasks.Task.FromResult(ContentSceneLoadResult.Success(sceneId, sceneName, string.Empty));
            }
        }
    }
}
