using System.Collections.Generic;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.UI.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ApexRivals.Editor
{
    public static class FunctionalUiSetup
    {
        private const string MainMenuScenePath = "Assets/ApexRivals/Scenes/MainMenu.unity";
        private const string GarageScenePath = "Assets/ApexRivals/Scenes/Garage.unity";
        private const string RaceScenePath = "Assets/ApexRivals/Scenes/Race.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string FunctionalUiRootName = "FunctionalUI";

        [MenuItem("Tools/Apex Rivals/Demo Setup/Create Functional UI")]
        public static void CreateFunctionalUi()
        {
            var report = new FunctionalUiReport("Functional UI setup");
            ConfigureMainMenu(report);
            ConfigureGarage(report);
            ConfigureRace(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LogReport(report);
        }

        public static void CreateFunctionalUiFromCommandLine()
        {
            CreateFunctionalUi();
            var validation = ValidateFunctionalUi();
            LogReport(validation);
            if (validation.HasErrors)
            {
                throw new System.InvalidOperationException(validation.CreateSummary());
            }
        }

        [MenuItem("Tools/Apex Rivals/Demo Setup/Validate Functional UI")]
        public static void ValidateFunctionalUiMenu()
        {
            LogReport(ValidateFunctionalUi());
        }

        public static void ValidateFunctionalUiFromCommandLine()
        {
            var report = ValidateFunctionalUi();
            LogReport(report);
            if (report.HasErrors)
            {
                throw new System.InvalidOperationException(report.CreateSummary());
            }
        }

        private static void ConfigureMainMenu(FunctionalUiReport report)
        {
            var scene = OpenScene(MainMenuScenePath, report);
            if (!scene.IsValid())
            {
                return;
            }

            if (FindComponentInScene<MainMenuUiSceneInstaller>(scene) != null)
            {
                report.AddSkipped("MainMenu functional UI already exists.");
                return;
            }

            var root = CreateSceneChild(scene, FunctionalUiRootName);
            var canvas = CreateCanvas(root.transform);
            var navigator = canvas.gameObject.AddComponent<UguiScreenNavigator>();
            var mainPanel = CreatePanel(canvas.transform, "MainPanel", true);
            var vehiclePanel = CreatePanel(canvas.transform, "VehicleSelectionPanel", false);
            var settingsPanel = CreatePanel(canvas.transform, "SettingsPanel", false);
            var mainView = mainPanel.AddComponent<MainMenuUguiView>();
            var vehicleView = vehiclePanel.AddComponent<VehicleSelectionUguiView>();
            var settingsView = settingsPanel.AddComponent<SettingsUguiView>();
            var installer = root.AddComponent<MainMenuUiSceneInstaller>();

            var title = CreateText(mainPanel.transform, "TitleText", "Apex Rivals", 52);
            var start = CreateButton(mainPanel.transform, "StartButton", "Select Vehicle");
            var garage = CreateButton(mainPanel.transform, "GarageButton", "Garage");
            var settings = CreateButton(mainPanel.transform, "SettingsButton", "Settings");
            var quit = CreateButton(mainPanel.transform, "QuitButton", "Quit");
            var blocker = CreateBlocker(mainPanel.transform, "LoadingBlocker", "Loading...");
            var error = CreateText(mainPanel.transform, "ErrorText", string.Empty, 24);

            var vehicleName = CreateText(vehiclePanel.transform, "VehicleNameText", "Vehicle", 36);
            var stats = CreateText(vehiclePanel.transform, "StatsText", string.Empty, 24);
            var previous = CreateButton(vehiclePanel.transform, "PreviousButton", "Previous");
            var next = CreateButton(vehiclePanel.transform, "NextButton", "Next");
            var confirm = CreateButton(vehiclePanel.transform, "ConfirmButton", "Confirm");
            var vehicleStatus = CreateText(vehiclePanel.transform, "VehicleStatusText", string.Empty, 22);

            var resolution = CreateDropdown(settingsPanel.transform, "ResolutionDropdown");
            var fullScreen = CreateToggle(settingsPanel.transform, "FullScreenToggle", "Fullscreen");
            var master = CreateSlider(settingsPanel.transform, "MasterVolumeSlider", "Master Volume");
            var music = CreateSlider(settingsPanel.transform, "MusicVolumeSlider", "Music Volume");
            var sfx = CreateSlider(settingsPanel.transform, "SfxVolumeSlider", "SFX Volume");
            var apply = CreateButton(settingsPanel.transform, "ApplyButton", "Apply");
            var cancel = CreateButton(settingsPanel.transform, "CancelButton", "Cancel");
            var restore = CreateButton(settingsPanel.transform, "RestoreDefaultsButton", "Restore Defaults");
            var settingsStatus = CreateText(settingsPanel.transform, "SettingsStatusText", string.Empty, 22);

            SetReference(mainView, "titleText", title);
            SetReference(mainView, "startButton", start);
            SetReference(mainView, "garageButton", garage);
            SetReference(mainView, "settingsButton", settings);
            SetReference(mainView, "quitButton", quit);
            SetReference(mainView, "loadingBlocker", blocker);
            SetReference(mainView, "errorText", error);

            SetReference(vehicleView, "vehicleNameText", vehicleName);
            SetReference(vehicleView, "statsText", stats);
            SetReference(vehicleView, "previousButton", previous);
            SetReference(vehicleView, "nextButton", next);
            SetReference(vehicleView, "confirmButton", confirm);
            SetReference(vehicleView, "statusText", vehicleStatus);

            SetReference(settingsView, "resolutionDropdown", resolution);
            SetReference(settingsView, "fullScreenToggle", fullScreen);
            SetReference(settingsView, "masterVolumeSlider", master);
            SetReference(settingsView, "musicVolumeSlider", music);
            SetReference(settingsView, "sfxVolumeSlider", sfx);
            SetReference(settingsView, "applyButton", apply);
            SetReference(settingsView, "cancelButton", cancel);
            SetReference(settingsView, "restoreDefaultsButton", restore);
            SetReference(settingsView, "statusText", settingsStatus);

            SetNavigator(navigator, mainPanel, vehiclePanel, settingsPanel, null, null, null, null, start, confirm, resolution, null, null, null);
            SetReference(installer, "screenNavigator", navigator);
            SetReference(installer, "mainMenuView", mainView);
            SetReference(installer, "vehicleSelectionView", vehicleView);
            SetReference(installer, "settingsView", settingsView);
            EnsureEventSystem(scene, start.gameObject, report);
            SaveScene(scene);
            report.AddCreated("MainMenu functional UI created.");
        }

        private static void ConfigureGarage(FunctionalUiReport report)
        {
            var scene = OpenScene(GarageScenePath, report);
            if (!scene.IsValid())
            {
                return;
            }

            if (FindComponentInScene<GarageUiSceneInstaller>(scene) != null)
            {
                report.AddSkipped("Garage functional UI already exists.");
                return;
            }

            var root = CreateSceneChild(scene, FunctionalUiRootName);
            var canvas = CreateCanvas(root.transform);
            var navigator = canvas.gameObject.AddComponent<UguiScreenNavigator>();
            var panel = CreatePanel(canvas.transform, "GaragePanel", true);
            var view = panel.AddComponent<GarageUguiView>();
            var installer = root.AddComponent<GarageUiSceneInstaller>();

            var currency = CreateText(panel.transform, "CurrencyText", "Currency: 0", 28);
            var vehicle = CreateText(panel.transform, "VehicleNameText", "Vehicle", 36);
            var engineLevel = CreateText(panel.transform, "EngineLevelText", "Engine: 0/0", 24);
            var enginePrice = CreateText(panel.transform, "EnginePriceText", "Next: 0", 22);
            var engineButton = CreateButton(panel.transform, "EngineUpgradeButton", "Upgrade Engine");
            var handlingLevel = CreateText(panel.transform, "HandlingLevelText", "Handling: 0/0", 24);
            var handlingPrice = CreateText(panel.transform, "HandlingPriceText", "Next: 0", 22);
            var handlingButton = CreateButton(panel.transform, "HandlingUpgradeButton", "Upgrade Handling");
            var stats = CreateText(panel.transform, "StatsText", string.Empty, 22);
            var start = CreateButton(panel.transform, "StartRaceButton", "Start Race");
            var back = CreateButton(panel.transform, "ReturnToMainMenuButton", "Main Menu");
            var status = CreateText(panel.transform, "GarageStatusText", string.Empty, 22);

            SetReference(view, "currencyText", currency);
            SetReference(view, "vehicleNameText", vehicle);
            SetReference(view, "engineLevelText", engineLevel);
            SetReference(view, "enginePriceText", enginePrice);
            SetReference(view, "engineUpgradeButton", engineButton);
            SetReference(view, "handlingLevelText", handlingLevel);
            SetReference(view, "handlingPriceText", handlingPrice);
            SetReference(view, "handlingUpgradeButton", handlingButton);
            SetReference(view, "statsText", stats);
            SetReference(view, "startRaceButton", start);
            SetReference(view, "returnToMainMenuButton", back);
            SetReference(view, "statusText", status);
            SetNavigator(navigator, null, null, null, panel, null, null, null, null, null, null, start, null, null);
            SetReference(installer, "screenNavigator", navigator);
            SetReference(installer, "garageView", view);
            EnsureEventSystem(scene, start.gameObject, report);
            SaveScene(scene);
            report.AddCreated("Garage functional UI created.");
        }

        private static void ConfigureRace(FunctionalUiReport report)
        {
            var scene = OpenScene(RaceScenePath, report);
            if (!scene.IsValid())
            {
                return;
            }

            if (FindComponentInScene<RaceUiSceneInstaller>(scene) != null)
            {
                report.AddSkipped("Race functional UI already exists.");
                return;
            }

            var root = CreateSceneChild(scene, FunctionalUiRootName);
            var canvas = CreateCanvas(root.transform);
            var navigator = canvas.gameObject.AddComponent<UguiScreenNavigator>();
            var hudPanel = CreatePanel(canvas.transform, "RaceHudPanel", true);
            var pausePanel = CreatePanel(canvas.transform, "PausePanel", false);
            var resultsPanel = CreatePanel(canvas.transform, "ResultsPanel", false);
            var hud = hudPanel.AddComponent<RaceHudUguiView>();
            var pause = pausePanel.AddComponent<PauseUguiView>();
            var results = resultsPanel.AddComponent<ResultsUguiView>();
            var installer = root.AddComponent<RaceUiSceneInstaller>();

            var countdown = CreateText(hudPanel.transform, "CountdownText", string.Empty, 64);
            var lap = CreateText(hudPanel.transform, "LapText", "Lap 0/0", 24);
            var position = CreateText(hudPanel.transform, "PositionText", "Position 0/0", 24);
            var time = CreateText(hudPanel.transform, "TimeText", "00:00.000", 24);

            var resume = CreateButton(pausePanel.transform, "ResumeButton", "Resume");
            var pauseRetry = CreateButton(pausePanel.transform, "RetryButton", "Retry");
            var pauseMain = CreateButton(pausePanel.transform, "ReturnToMainMenuButton", "Main Menu");
            var pauseStatus = CreateText(pausePanel.transform, "PauseStatusText", string.Empty, 22);

            var finalPosition = CreateText(resultsPanel.transform, "FinalPositionText", "Position 0/0", 30);
            var raceTime = CreateText(resultsPanel.transform, "RaceTimeText", "00:00.000", 24);
            var reward = CreateText(resultsPanel.transform, "RewardText", "Reward: 0", 24);
            var updatedCurrency = CreateText(resultsPanel.transform, "UpdatedCurrencyText", "Currency: 0", 24);
            var resultsRetry = CreateButton(resultsPanel.transform, "RetryButton", "Retry");
            var resultsGarage = CreateButton(resultsPanel.transform, "ReturnToGarageButton", "Garage");
            var resultsMain = CreateButton(resultsPanel.transform, "ReturnToMainMenuButton", "Main Menu");
            var resultsStatus = CreateText(resultsPanel.transform, "ResultsStatusText", string.Empty, 22);

            SetReference(hud, "countdownText", countdown);
            SetReference(hud, "lapText", lap);
            SetReference(hud, "positionText", position);
            SetReference(hud, "timeText", time);
            SetReference(pause, "resumeButton", resume);
            SetReference(pause, "retryButton", pauseRetry);
            SetReference(pause, "returnToMainMenuButton", pauseMain);
            SetReference(pause, "statusText", pauseStatus);
            SetReference(results, "finalPositionText", finalPosition);
            SetReference(results, "raceTimeText", raceTime);
            SetReference(results, "rewardText", reward);
            SetReference(results, "updatedCurrencyText", updatedCurrency);
            SetReference(results, "retryButton", resultsRetry);
            SetReference(results, "returnToGarageButton", resultsGarage);
            SetReference(results, "returnToMainMenuButton", resultsMain);
            SetReference(results, "statusText", resultsStatus);

            SetNavigator(navigator, null, null, null, null, hudPanel, pausePanel, resultsPanel, null, null, null, null, resume, resultsRetry);
            SetReference(installer, "screenNavigator", navigator);
            SetReference(installer, "raceSessionInstaller", FindComponentInScene<RaceSessionSceneInstaller>(scene));
            SetReference(installer, "hudView", hud);
            SetReference(installer, "pauseView", pause);
            SetReference(installer, "resultsView", results);
            EnsureEventSystem(scene, resume.gameObject, report);
            SaveScene(scene);
            report.AddCreated("Race functional UI created.");
        }

        private static FunctionalUiReport ValidateFunctionalUi()
        {
            var report = new FunctionalUiReport("Functional UI validation");
            ValidateScene<MainMenuUiSceneInstaller>(MainMenuScenePath, report, false);
            ValidateScene<GarageUiSceneInstaller>(GarageScenePath, report, false);
            ValidateScene<RaceUiSceneInstaller>(RaceScenePath, report, true);
            report.AddSkipped("Final Figma styling, animations, art, audio and 3D previews remain deferred.");
            return report;
        }

        private static void ValidateScene<TInstaller>(string path, FunctionalUiReport report, bool requireHiddenRacePanels)
            where TInstaller : Component
        {
            var scene = OpenScene(path, report);
            if (!scene.IsValid())
            {
                return;
            }

            var eventSystems = FindComponentsInScene<EventSystem>(scene);
            if (eventSystems.Count != 1)
            {
                report.AddError($"{path} must contain exactly one EventSystem.");
            }
            else
            {
                if (eventSystems[0].GetComponent<InputSystemUIInputModule>() == null)
                {
                    report.AddError($"{path} EventSystem must use InputSystemUIInputModule.");
                }

                if (eventSystems[0].GetComponent<StandaloneInputModule>() != null)
                {
                    report.AddError($"{path} must not contain StandaloneInputModule.");
                }
            }

            if (FindComponentInScene<Canvas>(scene) == null)
            {
                report.AddError($"{path} is missing Canvas.");
            }

            if (FindComponentInScene<TInstaller>(scene) == null)
            {
                report.AddError($"{path} is missing {typeof(TInstaller).Name}.");
            }
            else
            {
                ValidateKnownReferences(path, scene, report);
            }

            if (FindComponentInScene<GameBootstrapper>(scene) != null)
            {
                report.AddError($"{path} must not contain GameBootstrapper.");
            }

            if (requireHiddenRacePanels)
            {
                var pause = FindNamedRootChild(scene, "PausePanel");
                var results = FindNamedRootChild(scene, "ResultsPanel");
                if (pause != null && pause.activeSelf)
                {
                    report.AddError("Pause panel must start hidden.");
                }

                if (results != null && results.activeSelf)
                {
                    report.AddError("Results panel must start hidden.");
                }
            }
        }

        private static Scene OpenScene(string path, FunctionalUiReport report)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (sceneAsset == null)
            {
                report.AddError($"Missing scene: {path}");
                return default;
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static GameObject CreateSceneChild(Scene scene, string name)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create Functional UI");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            var canvasObject = CreateUiObject(parent, "FunctionalCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Stretch(canvasObject.GetComponent<RectTransform>());
            return canvas;
        }

        private static GameObject CreatePanel(Transform parent, string name, bool active)
        {
            var panel = CreateUiObject(parent, name);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.07f, 0.08f, 0.09f, 0.92f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 48, 48);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            Stretch(panel.GetComponent<RectTransform>());
            panel.SetActive(active);
            return panel;
        }

        private static Text CreateText(Transform parent, string name, string text, int size)
        {
            var textObject = CreateUiObject(parent, name);
            var label = textObject.AddComponent<Text>();
            label.font = GetDefaultFont();
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = text;
            var layout = textObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 720f;
            layout.preferredHeight = Mathf.Max(40f, size * 1.6f);
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var buttonObject = CreateUiObject(parent, name);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.19f, 0.24f, 0.28f, 1f);
            var button = buttonObject.AddComponent<Button>();
            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 420f;
            layout.preferredHeight = 56f;
            var text = CreateText(buttonObject.transform, "Text", label, 24);
            Stretch(text.GetComponent<RectTransform>());
            return button;
        }

        private static Dropdown CreateDropdown(Transform parent, string name)
        {
            var dropdownObject = DefaultControls.CreateDropdown(CreateDefaultControlResources());
            Undo.RegisterCreatedObjectUndo(dropdownObject, "Create Functional UI");
            dropdownObject.name = name;
            dropdownObject.transform.SetParent(parent, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            var layout = dropdownObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 520f;
            layout.preferredHeight = 56f;
            return dropdown;
        }

        private static Toggle CreateToggle(Transform parent, string name, string label)
        {
            var toggleObject = CreateUiObject(parent, name);
            var toggle = toggleObject.AddComponent<Toggle>();
            var layout = toggleObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 420f;
            layout.preferredHeight = 48f;
            var backgroundObject = CreateUiObject(toggleObject.transform, "Background");
            var background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0.19f, 0.24f, 0.28f, 1f);
            var checkmarkObject = CreateUiObject(backgroundObject.transform, "Checkmark");
            var checkmark = checkmarkObject.AddComponent<Image>();
            checkmark.color = Color.white;
            var text = CreateText(toggleObject.transform, "Label", label, 22);
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            SetRect(backgroundObject.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(32f, 32f));
            SetRect(checkmarkObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetRect(text.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 0f), Vector2.zero);
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, string name, string label)
        {
            CreateText(parent, name + "Label", label, 20);
            var sliderObject = CreateUiObject(parent, name);
            var slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            var layout = sliderObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 520f;
            layout.preferredHeight = 32f;
            var backgroundObject = CreateUiObject(sliderObject.transform, "Background");
            var background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            var fillObject = CreateUiObject(sliderObject.transform, "Fill");
            var fill = fillObject.AddComponent<Image>();
            fill.color = new Color(0.75f, 0.82f, 0.88f, 1f);
            var handleObject = CreateUiObject(sliderObject.transform, "Handle");
            var handle = handleObject.AddComponent<Image>();
            handle.color = Color.white;
            Stretch(backgroundObject.GetComponent<RectTransform>());
            Stretch(fillObject.GetComponent<RectTransform>());
            SetRect(handleObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 32f));
            slider.fillRect = fillObject.GetComponent<RectTransform>();
            slider.handleRect = handleObject.GetComponent<RectTransform>();
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static GameObject CreateBlocker(Transform parent, string name, string label)
        {
            var blocker = CreatePanel(parent, name, false);
            CreateText(blocker.transform, "Text", label, 28);
            return blocker;
        }

        private static GameObject CreateUiObject(Transform parent, string name)
        {
            var target = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(target, "Create Functional UI");
            target.transform.SetParent(parent, false);
            return target;
        }

        private static void EnsureEventSystem(Scene scene, GameObject firstSelected, FunctionalUiReport report)
        {
            var eventSystems = FindComponentsInScene<EventSystem>(scene);
            EventSystem eventSystem;
            if (eventSystems.Count == 0)
            {
                var eventSystemObject = CreateSceneChild(scene, "EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                report.AddCreated($"{scene.name} EventSystem created.");
            }
            else
            {
                eventSystem = eventSystems[0];
                if (eventSystems.Count > 1)
                {
                    report.AddError($"{scene.name} has duplicate EventSystems.");
                }
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Undo.DestroyObjectImmediate(standalone);
            }

            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            }

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions != null)
            {
                module.actionsAsset = actions;
            }
            else
            {
                report.AddError("Input action asset is missing.");
            }

            eventSystem.firstSelectedGameObject = firstSelected;
            EditorUtility.SetDirty(eventSystem);
            EditorUtility.SetDirty(module);
        }

        private static void SetNavigator(
            UguiScreenNavigator navigator,
            GameObject main,
            GameObject vehicle,
            GameObject settings,
            GameObject garage,
            GameObject racing,
            GameObject pause,
            GameObject results,
            Selectable mainInitial,
            Selectable vehicleInitial,
            Selectable settingsInitial,
            Selectable garageInitial,
            Selectable pauseInitial,
            Selectable resultsInitial)
        {
            SetReference(navigator, "mainPanel", main);
            SetReference(navigator, "vehicleSelectionPanel", vehicle);
            SetReference(navigator, "settingsPanel", settings);
            SetReference(navigator, "garagePanel", garage);
            SetReference(navigator, "racingPanel", racing);
            SetReference(navigator, "pausePanel", pause);
            SetReference(navigator, "resultsPanel", results);
            SetReference(navigator, "mainInitialSelection", mainInitial);
            SetReference(navigator, "vehicleSelectionInitialSelection", vehicleInitial);
            SetReference(navigator, "settingsInitialSelection", settingsInitial);
            SetReference(navigator, "garageInitialSelection", garageInitial);
            SetReference(navigator, "pauseInitialSelection", pauseInitial);
            SetReference(navigator, "resultsInitialSelection", resultsInitial);
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static DefaultControls.Resources CreateDefaultControlResources()
        {
            return new DefaultControls.Resources();
        }

        private static void Stretch(RectTransform rectTransform)
        {
            SetRect(rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
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

        private static List<T> FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            var components = new List<T>();
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                roots[index].GetComponentsInChildren(true, components);
            }

            return components;
        }

        private static GameObject FindNamedRootChild(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (var index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index].name == name)
                    {
                        return transforms[index].gameObject;
                    }
                }
            }

            return null;
        }

        private static void ValidateKnownReferences(string path, Scene scene, FunctionalUiReport report)
        {
            var mainInstaller = FindComponentInScene<MainMenuUiSceneInstaller>(scene);
            if (mainInstaller != null)
            {
                RequireReference(path, mainInstaller, "screenNavigator", report);
                RequireReference(path, mainInstaller, "mainMenuView", report);
                RequireReference(path, mainInstaller, "vehicleSelectionView", report);
                RequireReference(path, mainInstaller, "settingsView", report);
                ValidateView(path, FindComponentInScene<MainMenuUguiView>(scene), report, "titleText", "startButton", "garageButton", "settingsButton", "quitButton", "loadingBlocker", "errorText");
                ValidateView(path, FindComponentInScene<VehicleSelectionUguiView>(scene), report, "vehicleNameText", "statsText", "previousButton", "nextButton", "confirmButton", "statusText");
                ValidateView(path, FindComponentInScene<SettingsUguiView>(scene), report, "resolutionDropdown", "fullScreenToggle", "masterVolumeSlider", "musicVolumeSlider", "sfxVolumeSlider", "applyButton", "cancelButton", "restoreDefaultsButton", "statusText");
            }

            var garageInstaller = FindComponentInScene<GarageUiSceneInstaller>(scene);
            if (garageInstaller != null)
            {
                RequireReference(path, garageInstaller, "screenNavigator", report);
                RequireReference(path, garageInstaller, "garageView", report);
                ValidateView(path, FindComponentInScene<GarageUguiView>(scene), report, "currencyText", "vehicleNameText", "engineLevelText", "enginePriceText", "engineUpgradeButton", "handlingLevelText", "handlingPriceText", "handlingUpgradeButton", "statsText", "startRaceButton", "returnToMainMenuButton", "statusText");
            }

            var raceInstaller = FindComponentInScene<RaceUiSceneInstaller>(scene);
            if (raceInstaller != null)
            {
                RequireReference(path, raceInstaller, "screenNavigator", report);
                RequireReference(path, raceInstaller, "raceSessionInstaller", report);
                RequireReference(path, raceInstaller, "hudView", report);
                RequireReference(path, raceInstaller, "pauseView", report);
                RequireReference(path, raceInstaller, "resultsView", report);
                ValidateView(path, FindComponentInScene<RaceHudUguiView>(scene), report, "countdownText", "lapText", "positionText", "timeText");
                ValidateView(path, FindComponentInScene<PauseUguiView>(scene), report, "resumeButton", "retryButton", "returnToMainMenuButton", "statusText");
                ValidateView(path, FindComponentInScene<ResultsUguiView>(scene), report, "finalPositionText", "raceTimeText", "rewardText", "updatedCurrencyText", "retryButton", "returnToGarageButton", "returnToMainMenuButton", "statusText");
            }
        }

        private static void ValidateView(string path, Component component, FunctionalUiReport report, params string[] propertyNames)
        {
            if (component == null)
            {
                report.AddError($"{path} is missing expected view adapter.");
                return;
            }

            for (var index = 0; index < propertyNames.Length; index++)
            {
                RequireReference(path, component, propertyNames[index], report);
            }
        }

        private static void RequireReference(string path, Object target, string propertyName, FunctionalUiReport report)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                report.AddError($"{path}: {target.name} has no {propertyName} assigned.");
            }
        }

        private static void LogReport(FunctionalUiReport report)
        {
            if (report.HasErrors)
            {
                Debug.LogError(report.CreateSummary());
                return;
            }

            Debug.Log(report.CreateSummary());
        }

        private sealed class FunctionalUiReport
        {
            private readonly string _title;
            private readonly List<string> _created = new List<string>();
            private readonly List<string> _skipped = new List<string>();
            private readonly List<string> _errors = new List<string>();

            public FunctionalUiReport(string title)
            {
                _title = title;
            }

            public bool HasErrors => _errors.Count > 0;

            public void AddCreated(string message)
            {
                _created.Add(message);
            }

            public void AddSkipped(string message)
            {
                _skipped.Add(message);
            }

            public void AddError(string message)
            {
                _errors.Add(message);
            }

            public string CreateSummary()
            {
                var lines = new List<string> { _title + ":" };
                lines.Add(_created.Count == 0 ? "Created: none" : "Created:");
                lines.AddRange(_created);
                lines.Add(_skipped.Count == 0 ? "Skipped: none" : "Skipped:");
                lines.AddRange(_skipped);
                lines.Add(_errors.Count == 0 ? "Errors: none" : "Errors:");
                lines.AddRange(_errors);
                return string.Join("\n", lines);
            }
        }
    }
}
