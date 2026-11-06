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
using UnityEditor;
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
            Assert.That(result.Output.Opponents.Count, Is.EqualTo(1));
            Assert.That(result.Output.Participants.Count, Is.EqualTo(2));
            Assert.That(result.Output.DrivingGates.Count, Is.EqualTo(2));
            Assert.That(result.Output.PlayerCameraTarget, Is.Not.Null);
        }

        [Test]
        public void SetupRace_OnePlayerAndThreeAi_ProducesFourRegisteredGatedParticipantsInGridOrder()
        {
            var context = CreateContext(opponentCount: 3);

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Output.Player.RaceParticipant.RacerId, Is.EqualTo("Player"));
            Assert.That(result.Output.Opponents.Count, Is.EqualTo(3));
            Assert.That(result.Output.Participants.Count, Is.EqualTo(4));
            Assert.That(result.Output.DrivingGates.Count, Is.EqualTo(4));
            Assert.That(context.Factory.SpawnPoses.Count, Is.EqualTo(4));

            for (var index = 0; index < result.Output.Participants.Count; index++)
            {
                Assert.That(result.Output.Participants[index].RacerId, Is.EqualTo(index == 0 ? "Player" : $"AI_{index:00}"));
                Assert.That(context.Factory.SpawnPoses[index].Position, Is.EqualTo(new Vector3(index * 10f, 0f, 0f)));
                Assert.That(result.Output.DrivingGates[index].DrivingAllowed, Is.False);
                Assert.That(context.RaceCoordinator.TryGetProgress(result.Output.Participants[index], out _), Is.True);
            }

            for (var index = 0; index < result.Output.Opponents.Count; index++)
            {
                Assert.That(result.Output.Opponents[index].AiInputProvider, Is.Not.Null);
                Assert.That(result.Output.Opponents[index].AiInputProvider.Configuration, Is.Not.Null);
            }
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
        public void SetupRace_DuplicateStartingGridSpawnPoint_FailsBeforeSpawning()
        {
            var context = CreateContext();
            var sharedSpawnPoint = new GameObject("SharedSpawnPoint").transform;
            context.StartingGrid.Configure(new[] { sharedSpawnPoint, sharedSpawnPoint });

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Status, Is.EqualTo(RaceSetupStatus.InvalidStartingGrid));
            Assert.That(result.Message, Does.Contain("duplicate spawn point"));
            Assert.That(context.Factory.SpawnPoses, Is.Empty);
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

            var stats = result.Output.Player.VehicleRuntime.CurrentPerformanceStats;
            Assert.That(stats.Acceleration, Is.EqualTo(15f));
            Assert.That(stats.Steering, Is.EqualTo(42f));
        }

        [Test]
        public void SetupRace_RecompositionDoesNotAccumulateUpgrades()
        {
            var first = CreateContext(engineLevel: 1, handlingLevel: 1);
            var second = CreateContext(engineLevel: 1, handlingLevel: 1);

            var firstResult = first.Service.SetupRace(first.Roster, first.StartingGrid, first.RaceCoordinator, first.RacingLine);
            var secondResult = second.Service.SetupRace(second.Roster, second.StartingGrid, second.RaceCoordinator, second.RacingLine);

            Assert.That(secondResult.Output.Player.VehicleRuntime.CurrentPerformanceStats.Acceleration, Is.EqualTo(firstResult.Output.Player.VehicleRuntime.CurrentPerformanceStats.Acceleration));
            Assert.That(secondResult.Output.Player.VehicleRuntime.CurrentPerformanceStats.Steering, Is.EqualTo(firstResult.Output.Player.VehicleRuntime.CurrentPerformanceStats.Steering));
        }

        [Test]
        public void SetupRace_AiDoesNotReceivePlayerUpgradesByDefault()
        {
            var context = CreateContext(engineLevel: 1, handlingLevel: 1);

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            var aiStats = result.Output.Opponents[0].VehicleRuntime.CurrentPerformanceStats;
            Assert.That(aiStats.Acceleration, Is.EqualTo(10f));
            Assert.That(aiStats.Steering, Is.EqualTo(30f));
        }

        [Test]
        public void SetupRace_UsesResolvedProgressionDifficultyForOpponents()
        {
            var resolvedConfiguration = ScriptableObject.CreateInstance<AiDriverConfiguration>();
            var context = CreateContext(raceDifficultyProvider: new FakeDifficultyProvider(resolvedConfiguration));

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Output.Opponents[0].AiInputProvider.Configuration, Is.SameAs(resolvedConfiguration));
        }

        [Test]
        public void SetupRace_AppliesResolvedProgressionDifficultyToMultipleOpponents()
        {
            var resolvedConfiguration = ScriptableObject.CreateInstance<AiDriverConfiguration>();
            var context = CreateContext(raceDifficultyProvider: new FakeDifficultyProvider(resolvedConfiguration), opponentCount: 2);

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Output.Opponents.Count, Is.EqualTo(2));
            Assert.That(result.Output.Opponents[0].AiInputProvider.Configuration, Is.SameAs(resolvedConfiguration));
            Assert.That(result.Output.Opponents[1].AiInputProvider.Configuration, Is.SameAs(resolvedConfiguration));
            Assert.That(result.Output.Opponents[0].RaceParticipant.RacerId, Is.Not.EqualTo(result.Output.Opponents[1].RaceParticipant.RacerId));
        }

        [Test]
        public void SetupRace_FailedDifficultyResolutionPreventsIncompleteSetup()
        {
            var context = CreateContext(raceDifficultyProvider: new FakeDifficultyProvider(null, "Difficulty failed."));

            var result = context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(result.Status, Is.EqualTo(RaceSetupStatus.DifficultyResolutionFailed));
            Assert.That(context.Factory.DestroyedCount, Is.EqualTo(2));
        }

        [Test]
        public void SetupRace_DoesNotAffectSceneFlowNavigation()
        {
            var context = CreateContext();
            var sceneFlow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), new FakeSceneLoader());

            context.Service.SetupRace(context.Roster, context.StartingGrid, context.RaceCoordinator, context.RacingLine);

            Assert.That(sceneFlow.State, Is.EqualTo(ApplicationState.Bootstrapping));
        }

        private static TestContext CreateContext(
            int engineLevel = 0,
            int handlingLevel = 0,
            IRaceDifficultyProvider raceDifficultyProvider = null,
            int opponentCount = 1)
        {
            var progression = new PlayerProgressionState();
            progression.SetUpgradeLevel(UpgradeType.Engine, engineLevel);
            progression.SetUpgradeLevel(UpgradeType.Handling, handlingLevel);

            var vehicleConfiguration = ScriptableObject.CreateInstance<WheelVehicleConfiguration>();
            vehicleConfiguration.performanceAcceleration = 10f;
            vehicleConfiguration.performanceTopSpeed = 20f;
            vehicleConfiguration.performanceSteering = 30f;

            var prefab = new GameObject("VehiclePrefab");
            var catalog = new VehicleCatalog(new[]
            {
                new VehicleDefinitionData("starter", "Starter", string.Empty, vehicleConfiguration, prefab, null, true)
            }, "starter");

            var factory = new FakeRaceVehicleFactory(vehicleConfiguration.CreatePerformanceStats());
            var service = new RaceSetupService(
                catalog,
                progression,
                CreateEngineDefinition(),
                CreateHandlingDefinition(),
                factory,
                new Dictionary<string, AiDriverConfiguration> { { "standard", ScriptableObject.CreateInstance<AiDriverConfiguration>() } },
                raceDifficultyProvider);

            var startingGridObject = new GameObject("StartingGrid");
            var startingGrid = startingGridObject.AddComponent<StartingGrid>();
            var spawnPoints = new Transform[opponentCount + 1];
            for (var i = 0; i < spawnPoints.Length; i++)
            {
                spawnPoints[i] = new GameObject($"Spawn{i}").transform;
                spawnPoints[i].position = new Vector3(i * 10f, 0f, 0f);
            }

            startingGrid.Configure(spawnPoints);

            var raceCoordinator = new GameObject("RaceCoordinator").AddComponent<RaceCoordinator>();
            var racingLine = new GameObject("RacingLine").AddComponent<RacingLine>();
            var entries = new List<RaceRosterEntry>
            {
                new RaceRosterEntry("Player", RaceEntryType.Player, "starter", 0, string.Empty)
            };

            for (var i = 0; i < opponentCount; i++)
            {
                entries.Add(new RaceRosterEntry($"AI_{i + 1:00}", RaceEntryType.AI, "starter", i + 1, "standard"));
            }

            var roster = new RaceRoster(entries);

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
            private readonly VehiclePerformanceStats _prefabBaseStats;
            private int _createCount;

            public FakeRaceVehicleFactory(VehiclePerformanceStats prefabBaseStats)
            {
                _prefabBaseStats = prefabBaseStats;
            }

            public int FailOnCreateNumber { get; set; }
            public int DestroyedCount { get; private set; }
            public List<SpawnPose> SpawnPoses { get; } = new List<SpawnPose>();

            public bool TryCreate(GameObject prefab, SpawnPose pose, out RaceVehicleComposition composition, out string message)
            {
                _createCount++;
                composition = null;
                if (FailOnCreateNumber == _createCount)
                {
                    message = "Spawn failed.";
                    return false;
                }

                SpawnPoses.Add(pose);
                composition = CreateComposition(_prefabBaseStats);
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

            private static RaceVehicleComposition CreateComposition(VehiclePerformanceStats prefabBaseStats)
            {
                var root = new GameObject("RaceVehicle");
                root.AddComponent<Rigidbody>();
                var controller = root.AddComponent<WheelArcadeVehicleController>();
                controller.ApplyPerformanceStats(prefabBaseStats);
                var resetter = root.AddComponent<VehicleResetter>();
                var resetterSerializedObject = new SerializedObject(resetter);
                resetterSerializedObject.FindProperty("vehicleRigidbody").objectReferenceValue = root.GetComponent<Rigidbody>();
                resetterSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                var participant = root.AddComponent<RaceParticipant>();
                var gate = root.AddComponent<DrivingInputGate>();
                var playerInput = root.AddComponent<PlayerDrivingInput>();
                var aiInput = root.AddComponent<AiDrivingInputProvider>();
                var composition = root.AddComponent<RaceVehicleComposition>();
                composition.ConfigureReferences(controller, resetter, participant, gate, playerInput, aiInput, root.transform, root.transform, root.transform, null);
                return composition;
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

        private sealed class FakeDifficultyProvider : IRaceDifficultyProvider
        {
            private readonly AiDriverConfiguration _configuration;
            private readonly string _message;

            public FakeDifficultyProvider(AiDriverConfiguration configuration, string message = "")
            {
                _configuration = configuration;
                _message = message;
            }

            public DifficultyTierResolutionResult ResolveCurrentTier()
            {
                return new DifficultyTierResolutionResult(true, new RaceProgressionTier("test", "Test", 0, "test"), string.Empty);
            }

            public bool TryGetCurrentAiConfiguration(out AiDriverConfiguration aiConfiguration, out string message)
            {
                aiConfiguration = _configuration;
                message = _message;
                return aiConfiguration != null;
            }
        }
    }
}
