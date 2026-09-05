using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.AI.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Garage.Configuration;
using ApexRivals.Progression.Configuration;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Configuration;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Configuration;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.SceneFlow.Configuration;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.VehicleSelection.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApexRivals.Editor
{
    public static class IntegrationPhaseOneSetup
    {
        private const string Root = "Assets/ApexRivals";
        private const string ScenesRoot = Root + "/Scenes";
        private const string ContentRoot = Root + "/Content";
        private const string ConfigRoot = ContentRoot + "/Configuration";
        private const string BootstrapScenePath = ScenesRoot + "/Bootstrap.unity";
        private const string MainMenuScenePath = ScenesRoot + "/MainMenu.unity";
        private const string GarageScenePath = ScenesRoot + "/Garage.unity";
        private const string RaceScenePath = ScenesRoot + "/Race.unity";
        private const string SceneFlowConfigPath = ConfigRoot + "/SceneFlow/SceneFlowConfiguration.asset";
        private const string VehicleConfigPath = ConfigRoot + "/Vehicles/StarterVehicleConfiguration.asset";
        private const string VehicleDefinitionPath = ConfigRoot + "/Vehicles/StarterVehicleDefinition.asset";
        private const string VehicleCatalogPath = ConfigRoot + "/Vehicles/VehicleCatalogDefinition.asset";
        private const string EngineUpgradePath = ConfigRoot + "/Garage/EngineUpgradeDefinition.asset";
        private const string HandlingUpgradePath = ConfigRoot + "/Garage/HandlingUpgradeDefinition.asset";
        private const string RewardPath = ConfigRoot + "/Progression/RaceRewardDefinition.asset";
        private const string RaceDefinitionPath = ConfigRoot + "/Race/DemoRaceDefinition.asset";
        private const string RaceSetupPath = ConfigRoot + "/Race/DemoRaceSetupDefinition.asset";
        private const string AiDriverPath = ConfigRoot + "/AI/StandardAiDriverConfiguration.asset";

        [MenuItem("Apex Rivals/Integration Phase 1/Setup Scene Skeletons")]
        public static void SetupSceneSkeletons()
        {
            EnsureFolders();
            var sceneFlowConfiguration = EnsureAsset<SceneFlowConfiguration>(SceneFlowConfigPath);
            var vehicleConfiguration = EnsureAsset<ArcadeVehicleConfiguration>(VehicleConfigPath);
            var vehicleDefinition = EnsureAsset<VehicleDefinition>(VehicleDefinitionPath);
            var vehicleCatalog = EnsureAsset<VehicleCatalogDefinition>(VehicleCatalogPath);
            var engineUpgrade = EnsureAsset<UpgradeDefinition>(EngineUpgradePath);
            var handlingUpgrade = EnsureAsset<UpgradeDefinition>(HandlingUpgradePath);
            var rewardDefinition = EnsureAsset<RaceRewardDefinition>(RewardPath);
            var raceDefinition = EnsureAsset<RaceDefinition>(RaceDefinitionPath);
            var raceSetup = EnsureAsset<RaceSetupDefinition>(RaceSetupPath);
            EnsureAsset<AiDriverConfiguration>(AiDriverPath);

            ConfigureVehicleDefinition(vehicleDefinition, vehicleConfiguration);
            ConfigureVehicleCatalog(vehicleCatalog, vehicleDefinition);
            ConfigureUpgrade(engineUpgrade, UpgradeType.Engine, CreateEngineLevels());
            ConfigureUpgrade(handlingUpgrade, UpgradeType.Handling, CreateHandlingLevels());
            ConfigureRaceSetup(raceSetup, raceDefinition);

            CreateBootstrapScene(sceneFlowConfiguration, vehicleCatalog, engineUpgrade, handlingUpgrade);
            CreateSimpleScene(MainMenuScenePath, "MainMenuSceneRoot");
            CreateSimpleScene(GarageScenePath, "GarageSceneRoot");
            CreateRaceScene(raceDefinition, raceSetup, rewardDefinition, engineUpgrade, handlingUpgrade);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var validation = ValidateProject();
            LogValidation(validation);
        }

        public static void SetupFromCommandLine()
        {
            SetupSceneSkeletons();
            var validation = ValidateProject();
            if (validation.HasErrors)
            {
                throw new System.InvalidOperationException(validation.CreateSummary());
            }
        }

        [MenuItem("Apex Rivals/Integration Phase 1/Validate Scene Skeletons")]
        public static void ValidateSceneSkeletons()
        {
            LogValidation(ValidateProject());
        }

        public static void ValidateFromCommandLine()
        {
            var validation = ValidateProject();
            LogValidation(validation);
            if (validation.HasErrors)
            {
                throw new System.InvalidOperationException(validation.CreateSummary());
            }
        }

        private static IntegrationValidationReport ValidateProject()
        {
            var report = new IntegrationValidationReport();
            RequireAsset<SceneFlowConfiguration>(SceneFlowConfigPath, report);
            RequireAsset<VehicleCatalogDefinition>(VehicleCatalogPath, report);
            RequireAsset<UpgradeDefinition>(EngineUpgradePath, report);
            RequireAsset<UpgradeDefinition>(HandlingUpgradePath, report);
            RequireAsset<RaceRewardDefinition>(RewardPath, report);
            RequireAsset<RaceDefinition>(RaceDefinitionPath, report);
            RequireAsset<RaceSetupDefinition>(RaceSetupPath, report);

            RequireScene(BootstrapScenePath, report);
            RequireScene(MainMenuScenePath, report);
            RequireScene(GarageScenePath, report);
            RequireScene(RaceScenePath, report);
            ValidateBuildSettings(report);

            var bootstrapScene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var bootstrapper = FindComponentInScene<GameBootstrapper>(bootstrapScene);
            if (bootstrapper == null)
            {
                report.AddError("Bootstrap scene is missing GameBootstrapper.");
            }
            else
            {
                ValidateSerializedReference(bootstrapper, "sceneFlowConfiguration", report);
                ValidateSerializedReference(bootstrapper, "vehicleCatalogDefinition", report);
                ValidateSerializedReference(bootstrapper, "engineUpgradeDefinition", report);
                ValidateSerializedReference(bootstrapper, "handlingUpgradeDefinition", report);
                ValidateSerializedReference(bootstrapper, "sceneLoader", report);
            }

            var raceScene = EditorSceneManager.OpenScene(RaceScenePath, OpenSceneMode.Single);
            var raceInstaller = FindComponentInScene<RaceSessionSceneInstaller>(raceScene);
            var raceCoordinator = FindComponentInScene<RaceCoordinator>(raceScene);
            var startingGrid = FindComponentInScene<StartingGrid>(raceScene);
            var racingLine = FindComponentInScene<RacingLine>(raceScene);

            if (raceInstaller == null)
            {
                report.AddError("Race scene is missing RaceSessionSceneInstaller.");
            }
            else
            {
                ValidateSerializedReference(raceInstaller, "raceSetupDefinition", report);
                ValidateSerializedReference(raceInstaller, "raceRewardDefinition", report);
                ValidateSerializedReference(raceInstaller, "engineUpgradeDefinition", report);
                ValidateSerializedReference(raceInstaller, "handlingUpgradeDefinition", report);
                ValidateSerializedReference(raceInstaller, "raceCoordinator", report);
                ValidateSerializedReference(raceInstaller, "startingGrid", report);
                ValidateSerializedReference(raceInstaller, "racingLine", report);
            }

            if (raceCoordinator == null)
            {
                report.AddError("Race scene is missing RaceCoordinator.");
            }

            if (startingGrid == null)
            {
                report.AddError("Race scene is missing StartingGrid.");
            }

            if (racingLine == null)
            {
                report.AddError("Race scene is missing RacingLine.");
            }

            report.AddWarning("Vehicle prefab references are intentionally deferred; Race setup will not spawn playable vehicles yet.");
            report.AddWarning("Final UI, circuit, art and audio content are intentionally deferred.");
            return report;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Content");
            EnsureFolder(ContentRoot, "Configuration");
            EnsureFolder(ConfigRoot, "SceneFlow");
            EnsureFolder(ConfigRoot, "Vehicles");
            EnsureFolder(ConfigRoot, "Garage");
            EnsureFolder(ConfigRoot, "Progression");
            EnsureFolder(ConfigRoot, "Race");
            EnsureFolder(ConfigRoot, "AI");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static T EnsureAsset<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureVehicleDefinition(VehicleDefinition vehicleDefinition, ArcadeVehicleConfiguration vehicleConfiguration)
        {
            var serialized = new SerializedObject(vehicleDefinition);
            serialized.FindProperty("stableId").stringValue = "starter";
            serialized.FindProperty("displayName").stringValue = "Starter";
            serialized.FindProperty("description").stringValue = "Initial demo vehicle.";
            serialized.FindProperty("baseConfiguration").objectReferenceValue = vehicleConfiguration;
            serialized.FindProperty("vehiclePrefab").objectReferenceValue = null;
            serialized.FindProperty("previewSprite").objectReferenceValue = null;
            serialized.FindProperty("availableInDemo").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vehicleDefinition);
        }

        private static void ConfigureVehicleCatalog(VehicleCatalogDefinition vehicleCatalog, VehicleDefinition vehicleDefinition)
        {
            var serialized = new SerializedObject(vehicleCatalog);
            serialized.FindProperty("defaultVehicleId").stringValue = "starter";
            var vehicles = serialized.FindProperty("vehicles");
            vehicles.arraySize = 1;
            vehicles.GetArrayElementAtIndex(0).objectReferenceValue = vehicleDefinition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vehicleCatalog);
        }

        private static void ConfigureUpgrade(UpgradeDefinition upgradeDefinition, UpgradeType type, UpgradeLevelDefinition[] levels)
        {
            var serialized = new SerializedObject(upgradeDefinition);
            serialized.FindProperty("upgradeType").enumValueIndex = (int)type;
            var serializedLevels = serialized.FindProperty("levels");
            serializedLevels.arraySize = levels.Length;
            for (var index = 0; index < levels.Length; index++)
            {
                var element = serializedLevels.GetArrayElementAtIndex(index);
                SetUpgradeLevel(element, levels[index]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(upgradeDefinition);
        }

        private static void SetUpgradeLevel(SerializedProperty element, UpgradeLevelDefinition level)
        {
            var runtimeLevel = level.ToRuntimeLevel();
            var modifier = runtimeLevel.StatModifier;
            element.FindPropertyRelative("price").intValue = runtimeLevel.Price;
            element.FindPropertyRelative("accelerationMultiplier").floatValue = modifier.AccelerationMultiplier;
            element.FindPropertyRelative("maximumSpeedMultiplier").floatValue = modifier.MaximumSpeedMultiplier;
            element.FindPropertyRelative("steeringMultiplier").floatValue = modifier.SteeringMultiplier;
            element.FindPropertyRelative("normalGripBonus").floatValue = modifier.NormalGripBonus;
            element.FindPropertyRelative("driftGripBonus").floatValue = modifier.DriftGripBonus;
        }

        private static UpgradeLevelDefinition[] CreateEngineLevels()
        {
            return new[]
            {
                new UpgradeLevelDefinition(200, 1.08f, 1.04f, 1f, 0f, 0f),
                new UpgradeLevelDefinition(350, 1.16f, 1.08f, 1f, 0f, 0f),
                new UpgradeLevelDefinition(500, 1.25f, 1.12f, 1f, 0f, 0f)
            };
        }

        private static UpgradeLevelDefinition[] CreateHandlingLevels()
        {
            return new[]
            {
                new UpgradeLevelDefinition(200, 1f, 1f, 1.08f, 0.03f, 0.02f),
                new UpgradeLevelDefinition(350, 1f, 1f, 1.16f, 0.06f, 0.04f),
                new UpgradeLevelDefinition(500, 1f, 1f, 1.25f, 0.1f, 0.08f)
            };
        }

        private static void ConfigureRaceSetup(RaceSetupDefinition raceSetup, RaceDefinition raceDefinition)
        {
            var serialized = new SerializedObject(raceSetup);
            serialized.FindProperty("raceDefinition").objectReferenceValue = raceDefinition;
            serialized.FindProperty("playerParticipantId").stringValue = "Player";
            serialized.FindProperty("playerVehicleId").stringValue = "starter";
            serialized.FindProperty("playerStartingGridIndex").intValue = 0;
            serialized.FindProperty("aiOpponents").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(raceSetup);
        }

        private static void CreateBootstrapScene(
            SceneFlowConfiguration sceneFlowConfiguration,
            VehicleCatalogDefinition vehicleCatalog,
            UpgradeDefinition engineUpgrade,
            UpgradeDefinition handlingUpgrade)
        {
            var scene = NewEmptyScene();
            var bootstrapObject = new GameObject("Bootstrap");
            var sceneLoader = bootstrapObject.AddComponent<UnityContentSceneLoader>();
            var bootstrapper = bootstrapObject.AddComponent<GameBootstrapper>();

            SetReference(bootstrapper, "sceneFlowConfiguration", sceneFlowConfiguration);
            SetReference(bootstrapper, "vehicleCatalogDefinition", vehicleCatalog);
            SetReference(bootstrapper, "engineUpgradeDefinition", engineUpgrade);
            SetReference(bootstrapper, "handlingUpgradeDefinition", handlingUpgrade);
            SetReference(bootstrapper, "sceneLoader", sceneLoader);
            SetStringArray(bootstrapper, "knownVehicleIds", new[] { "starter" });

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void CreateSimpleScene(string path, string rootName)
        {
            var scene = NewEmptyScene();
            new GameObject(rootName);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateRaceScene(
            RaceDefinition raceDefinition,
            RaceSetupDefinition raceSetup,
            RaceRewardDefinition rewardDefinition,
            UpgradeDefinition engineUpgrade,
            UpgradeDefinition handlingUpgrade)
        {
            var scene = NewEmptyScene();
            var root = new GameObject("RaceSceneRoot");

            var coordinatorObject = new GameObject("RaceCoordinator");
            coordinatorObject.transform.SetParent(root.transform);
            var coordinator = coordinatorObject.AddComponent<RaceCoordinator>();
            SetReference(coordinator, "raceDefinition", raceDefinition);
            SetBool(coordinator, "startCountdownOnEnable", false);

            var gridObject = new GameObject("StartingGrid");
            gridObject.transform.SetParent(root.transform);
            var grid = gridObject.AddComponent<StartingGrid>();
            var spawnPoint = new GameObject("PlayerSpawn");
            spawnPoint.transform.SetParent(gridObject.transform);
            spawnPoint.transform.localPosition = Vector3.zero;
            spawnPoint.transform.localRotation = Quaternion.identity;
            grid.Configure(new[] { spawnPoint.transform });

            var racingLineObject = new GameObject("RacingLine");
            racingLineObject.transform.SetParent(root.transform);
            var racingLine = racingLineObject.AddComponent<RacingLine>();
            ConfigureRacingLine(racingLineObject.transform, racingLine);

            var installerObject = new GameObject("RaceSessionInstaller");
            installerObject.transform.SetParent(root.transform);
            var installer = installerObject.AddComponent<RaceSessionSceneInstaller>();
            SetReference(installer, "raceSetupDefinition", raceSetup);
            SetReference(installer, "raceRewardDefinition", rewardDefinition);
            SetReference(installer, "engineUpgradeDefinition", engineUpgrade);
            SetReference(installer, "handlingUpgradeDefinition", handlingUpgrade);
            SetReference(installer, "raceCoordinator", coordinator);
            SetReference(installer, "startingGrid", grid);
            SetReference(installer, "racingLine", racingLine);

            EditorSceneManager.SaveScene(scene, RaceScenePath);
        }

        private static void ConfigureRacingLine(Transform parent, RacingLine racingLine)
        {
            var waypoints = new Transform[4];
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(20f, 0f, 0f),
                new Vector3(20f, 0f, 20f),
                new Vector3(0f, 0f, 20f)
            };

            for (var index = 0; index < waypoints.Length; index++)
            {
                var waypoint = new GameObject($"Waypoint_{index:00}");
                waypoint.transform.SetParent(parent);
                waypoint.transform.localPosition = positions[index];
                waypoints[index] = waypoint.transform;
            }

            var serialized = new SerializedObject(racingLine);
            var property = serialized.FindProperty("waypoints");
            property.arraySize = waypoints.Length;
            for (var index = 0; index < waypoints.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = waypoints[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GarageScenePath, true),
                new EditorBuildSettingsScene(RaceScenePath, true)
            };
        }

        private static Scene NewEmptyScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetStringArray(Object target, string propertyName, IReadOnlyList<string> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void RequireAsset<T>(string path, IntegrationValidationReport report)
            where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
            {
                report.AddError($"Missing asset: {path}");
            }
        }

        private static void RequireScene(string path, IntegrationValidationReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                report.AddError($"Missing scene: {path}");
            }
        }

        private static void ValidateBuildSettings(IntegrationValidationReport report)
        {
            var scenes = EditorBuildSettings.scenes;
            var expected = new[] { BootstrapScenePath, MainMenuScenePath, GarageScenePath, RaceScenePath };
            if (scenes.Length < expected.Length)
            {
                report.AddError("Build Settings are missing Integration Phase 1 scenes.");
                return;
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (!scenes[index].enabled || scenes[index].path != expected[index])
                {
                    report.AddError($"Build Settings entry {index} must be enabled and point to {expected[index]}.");
                }
            }
        }

        private static void ValidateSerializedReference(Object target, string propertyName, IntegrationValidationReport report)
        {
            var serialized = new SerializedObject(target);
            if (serialized.FindProperty(propertyName).objectReferenceValue == null)
            {
                report.AddError($"{target.name} has no {propertyName} assigned.");
            }
        }

        private static T FindComponentInScene<T>(Scene scene)
            where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                var component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void LogValidation(IntegrationValidationReport report)
        {
            if (report.HasErrors)
            {
                Debug.LogError(report.CreateSummary());
                return;
            }

            Debug.Log(report.CreateSummary());
        }

        private sealed class IntegrationValidationReport
        {
            private readonly List<string> _errors = new List<string>();
            private readonly List<string> _warnings = new List<string>();

            public bool HasErrors => _errors.Count > 0;

            public void AddError(string message)
            {
                _errors.Add(message);
            }

            public void AddWarning(string message)
            {
                _warnings.Add(message);
            }

            public string CreateSummary()
            {
                var lines = new List<string> { "Integration Phase 1 validation:" };
                lines.Add(_errors.Count == 0 ? "Errors: none" : "Errors:");
                lines.AddRange(_errors);
                lines.Add(_warnings.Count == 0 ? "Warnings: none" : "Warnings:");
                lines.AddRange(_warnings);
                return string.Join("\n", lines);
            }
        }
    }
}
