using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BootstrapUiInputSystem : MonoBehaviour
    {
        public const string PreviousTabActionName = "PreviousTab";
        public const string NextTabActionName = "NextTab";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private float moveRepeatDelay = 0.45f;
        [SerializeField] private float moveRepeatRate = 0.12f;
        [SerializeField, Tooltip("Enables mouse pointer interaction with the UI. Development kill-switch; keep enabled to mix mouse, keyboard and gamepad.")] private bool allowPointerInput = true;
        [SerializeField, Min(0f), Tooltip("Pixels the mouse must travel before keyboard/gamepad mode hands control back to the mouse. Filters sensor jitter.")] private float mouseActivationDistance = 4f;

        private readonly List<InputActionReference> _runtimeReferences = new List<InputActionReference>();
        private InputAction _navigateAction;
        private InputAction _submitAction;
        private InputAction _cancelAction;
        private InputAction _pointAction;
        private InputAction _clickAction;
        private InputAction _previousTabAction;
        private InputAction _nextTabAction;
        private IUiFocusCoordinator _focusCoordinator;
        private IUiVehicleSelectionNavigation _vehicleSelectionNavigation;
        private IUiTabNavigation _tabNavigation;
        private double _lastSubmitTime = double.NegativeInfinity;
        private double _lastCancelTime = double.NegativeInfinity;
        private Coroutine _deferredSubmit;
        private Vector2 _pointerAnchor;
        private bool _hasPointerAnchor;
        private bool _selectHoveredPending;
        private bool _moduleConfigured;
        private bool _configuredAllowPointerInput;
        private FullScreenMode _lastAppliedFullScreenMode;
        private bool _lastAppliedAllowPointerInput;
        private bool _lastAppliedGameplayActive;

        private void Awake()
        {
            UiInputModality.Set(UiInputMode.Navigation);
            UiFocus.Reset();
            EnsureComponents();
            ConfigureInputModule();
            DisableCompetingInputOwners(gameObject.scene);
            ApplyCursorPolicy();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyCursorPolicy();
            }
        }

        private void LateUpdate()
        {
            if (allowPointerInput != _configuredAllowPointerInput)
            {
                ConfigureInputModule();
            }

            if (Screen.fullScreenMode != _lastAppliedFullScreenMode
                || allowPointerInput != _lastAppliedAllowPointerInput
                || IsGameplayActive() != _lastAppliedGameplayActive)
            {
                ApplyCursorPolicy();
            }

            if (_selectHoveredPending)
            {
                // Runs after EventSystem.Update, so the module has already raycast this frame's position.
                _selectHoveredPending = false;
                SelectHoveredSelectable();
            }
        }

        private void ApplyCursorPolicy()
        {
            _lastAppliedFullScreenMode = Screen.fullScreenMode;
            _lastAppliedAllowPointerInput = allowPointerInput;
            _lastAppliedGameplayActive = IsGameplayActive();
            var policy = UiCursorPolicy.Resolve(allowPointerInput, UiInputModality.Current, _lastAppliedGameplayActive, _lastAppliedFullScreenMode);
            if (Cursor.visible == policy.visible && Cursor.lockState == policy.lockMode)
            {
                return;
            }

            Cursor.visible = policy.visible;
            Cursor.lockState = policy.lockMode;

            // Locking/unlocking can warp the cursor; that jump must not read as the player moving the mouse.
            _hasPointerAnchor = false;
        }

        private bool IsGameplayActive()
        {
            return _focusCoordinator is Object coordinator && coordinator != null && _focusCoordinator.IsGameplayActive;
        }

        private void SetModality(UiInputMode mode)
        {
            if (!allowPointerInput)
            {
                mode = UiInputMode.Navigation;
            }

            if (UiInputModality.Current == mode)
            {
                return;
            }

            UiInputModality.Set(mode);
            if (mode == UiInputMode.Mouse)
            {
                _selectHoveredPending = true;
                _focusCoordinator?.NotifyMouseInput();
            }
            else
            {
                _hasPointerAnchor = false;
            }

            ApplyCursorPolicy();
        }

        private void SelectHoveredSelectable()
        {
            var current = EventSystem.current;
            if (inputModule == null || current == null || current.alreadySelecting || Mouse.current == null)
            {
                return;
            }

            var hovered = inputModule.GetLastRaycastResult(Mouse.current.deviceId).gameObject;
            UiFocus.Request(hovered != null ? hovered.GetComponentInParent<Selectable>() : null);
        }

        private void OnDestroy()
        {
            if (_deferredSubmit != null)
            {
                StopCoroutine(_deferredSubmit);
                _deferredSubmit = null;
            }

            UnsubscribeUiActions();
            for (var index = 0; index < _runtimeReferences.Count; index++)
            {
                if (_runtimeReferences[index] != null)
                {
                    Destroy(_runtimeReferences[index]);
                }
            }

            _runtimeReferences.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DisableCompetingInputOwners(scene);
            RegisterFocusCoordinator(scene);
            ConfigureInputModule();
        }

        private void EnsureComponents()
        {
            if (eventSystem == null)
            {
                eventSystem = GetComponent<EventSystem>();
            }

            if (eventSystem == null)
            {
                eventSystem = gameObject.AddComponent<EventSystem>();
            }

            if (inputModule == null)
            {
                inputModule = GetComponent<InputSystemUIInputModule>();
            }

            if (inputModule == null)
            {
                inputModule = gameObject.AddComponent<InputSystemUIInputModule>();
            }

            var standaloneInput = GetComponent<StandaloneInputModule>();
            if (standaloneInput != null)
            {
                Destroy(standaloneInput);
            }
        }

        private void ConfigureInputModule()
        {
            var pointerSettingChanged = _configuredAllowPointerInput != allowPointerInput;
            _configuredAllowPointerInput = allowPointerInput;
            if (inputActions == null || inputModule == null)
            {
                return;
            }

            var uiMap = inputActions.FindActionMap("UI", false);
            if (uiMap == null)
            {
                Debug.LogError("Bootstrap UI input could not find the UI action map.", this);
                return;
            }

            if (_moduleConfigured && !pointerSettingChanged)
            {
                // Scene loads only need the map re-enabled. Rebuilding would destroy references the
                // module still holds, and swapping pointer actions makes the module disable them.
                uiMap.Enable();
                return;
            }

            if (!allowPointerInput)
            {
                UiInputModality.Set(UiInputMode.Navigation);
            }

            inputModule.actionsAsset = inputActions;
            inputModule.moveRepeatDelay = moveRepeatDelay;
            inputModule.moveRepeatRate = moveRepeatRate;
            ReleaseModuleActions();
            ClearRuntimeReferences();
            _moduleConfigured = true;
            inputModule.move = CreateReference(uiMap, "Navigate");
            inputModule.submit = null;
            inputModule.cancel = null;
            // Pointer actions stay bound for the whole session; the active device is tracked by
            // UiInputModality instead of unbinding Point, which used to disable the action itself.
            inputModule.point = allowPointerInput ? CreateReference(uiMap, "Point") : null;
            inputModule.leftClick = allowPointerInput ? CreateReference(uiMap, "Click") : null;
            inputModule.rightClick = allowPointerInput ? CreateReference(uiMap, "RightClick") : null;
            inputModule.middleClick = allowPointerInput ? CreateReference(uiMap, "MiddleClick") : null;
            inputModule.scrollWheel = allowPointerInput ? CreateReference(uiMap, "ScrollWheel") : null;

            SubscribeUiActions(uiMap);
            uiMap.Enable();
        }

        private InputActionReference CreateReference(InputActionMap uiMap, string actionName)
        {
            var action = uiMap.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogError($"Bootstrap UI input could not find UI/{actionName}.", this);
                return null;
            }

            var reference = InputActionReference.Create(action);
            _runtimeReferences.Add(reference);
            return reference;
        }

        private void ReleaseModuleActions()
        {
            inputModule.move = null;
            inputModule.point = null;
            inputModule.leftClick = null;
            inputModule.rightClick = null;
            inputModule.middleClick = null;
            inputModule.scrollWheel = null;
        }

        private void ClearRuntimeReferences()
        {
            UnsubscribeUiActions();
            for (var index = 0; index < _runtimeReferences.Count; index++)
            {
                if (_runtimeReferences[index] != null)
                {
                    Destroy(_runtimeReferences[index]);
                }
            }

            _runtimeReferences.Clear();
        }

        private void SubscribeUiActions(InputActionMap uiMap)
        {
            _navigateAction = FindRequiredAction(uiMap, "Navigate");
            _submitAction = FindRequiredAction(uiMap, "Submit");
            _cancelAction = FindRequiredAction(uiMap, "Cancel");
            _pointAction = allowPointerInput ? uiMap.FindAction("Point", false) : null;
            _clickAction = allowPointerInput ? uiMap.FindAction("Click", false) : null;
            _previousTabAction = FindRequiredAction(uiMap, PreviousTabActionName);
            _nextTabAction = FindRequiredAction(uiMap, NextTabActionName);

            if (_navigateAction != null)
            {
                _navigateAction.performed += OnNavigatePerformed;
            }

            if (_submitAction != null)
            {
                _submitAction.performed += OnSubmitPerformed;
            }

            if (_cancelAction != null)
            {
                _cancelAction.performed += OnCancelPerformed;
            }

            if (_pointAction != null)
            {
                _pointAction.performed += OnPointPerformed;
            }

            if (_clickAction != null)
            {
                _clickAction.performed += OnClickPerformed;
            }

            if (_previousTabAction != null)
            {
                _previousTabAction.performed += OnPreviousTabPerformed;
            }

            if (_nextTabAction != null)
            {
                _nextTabAction.performed += OnNextTabPerformed;
            }
        }

        private InputAction FindRequiredAction(InputActionMap uiMap, string actionName)
        {
            var action = uiMap.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogError($"Bootstrap UI input could not find UI/{actionName}.", this);
            }

            return action;
        }

        private void UnsubscribeUiActions()
        {
            if (_navigateAction != null)
            {
                _navigateAction.performed -= OnNavigatePerformed;
            }

            if (_submitAction != null)
            {
                _submitAction.performed -= OnSubmitPerformed;
            }

            if (_cancelAction != null)
            {
                _cancelAction.performed -= OnCancelPerformed;
            }

            if (_pointAction != null)
            {
                _pointAction.performed -= OnPointPerformed;
            }

            if (_clickAction != null)
            {
                _clickAction.performed -= OnClickPerformed;
            }

            if (_previousTabAction != null)
            {
                _previousTabAction.performed -= OnPreviousTabPerformed;
            }

            if (_nextTabAction != null)
            {
                _nextTabAction.performed -= OnNextTabPerformed;
            }

            _navigateAction = null;
            _submitAction = null;
            _cancelAction = null;
            _pointAction = null;
            _clickAction = null;
            _previousTabAction = null;
            _nextTabAction = null;
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValue<Vector2>().sqrMagnitude < 0.25f)
            {
                return;
            }

            SetModality(UiInputMode.Navigation);
            _focusCoordinator?.NotifyNavigationInput();
        }

        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            if (!context.ReadValueAsButton())
            {
                return;
            }

            if (IsDuplicateSubmit(context.time))
            {
                return;
            }

            SetModality(UiInputMode.Navigation);

            // Validate before notifying the coordinator: NotifyNavigationInput repairs focus, and a
            // submit must never activate a control the player did not have focused when pressing it.
            var hadValidSelection = HasValidSubmitTarget();
            _focusCoordinator?.NotifyNavigationInput();
            if (hadValidSelection && TrySubmitCurrentSelection())
            {
                return;
            }

            if (_deferredSubmit != null)
            {
                StopCoroutine(_deferredSubmit);
            }

            _deferredSubmit = StartCoroutine(RepairSelectionNextFrame());
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (!context.ReadValueAsButton())
            {
                return;
            }

            if (IsDuplicateCancel(context.time))
            {
                return;
            }

            SetModality(UiInputMode.Navigation);
            _focusCoordinator?.NotifyNavigationInput();
            _focusCoordinator?.NotifyCancelInput();
        }

        private void OnPreviousTabPerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValueAsButton())
            {
                SetModality(UiInputMode.Navigation);
                if (_vehicleSelectionNavigation != null)
                {
                    _vehicleSelectionNavigation.SelectPreviousVehicle();
                }
                else
                {
                    _tabNavigation?.ShowPreviousTab();
                }
            }
        }

        private void OnNextTabPerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValueAsButton())
            {
                SetModality(UiInputMode.Navigation);
                if (_vehicleSelectionNavigation != null)
                {
                    _vehicleSelectionNavigation.SelectNextVehicle();
                }
                else
                {
                    _tabNavigation?.ShowNextTab();
                }
            }
        }

        private bool IsDuplicateCancel(double inputTime)
        {
            if (inputTime - _lastCancelTime < 0.15d)
            {
                return true;
            }

            _lastCancelTime = inputTime;
            return false;
        }

        private bool IsDuplicateSubmit(double inputTime)
        {
            if (inputTime - _lastSubmitTime < 0.15d)
            {
                return true;
            }

            _lastSubmitTime = inputTime;
            return false;
        }

        private System.Collections.IEnumerator RepairSelectionNextFrame()
        {
            // Panels activated this frame may only become selectable on the next one.
            yield return null;

            _focusCoordinator?.NotifyNavigationInput();
            _deferredSubmit = null;
        }

        private static GameObject CurrentSubmitTarget()
        {
            return EventSystem.current != null
                ? ExecuteEvents.GetEventHandler<ISubmitHandler>(EventSystem.current.currentSelectedGameObject)
                : null;
        }

        private static bool HasValidSubmitTarget()
        {
            return IsValidSubmitTarget(CurrentSubmitTarget());
        }

        private static bool TrySubmitCurrentSelection()
        {
            var selected = CurrentSubmitTarget();
            if (!IsValidSubmitTarget(selected))
            {
                return false;
            }

            var eventData = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(selected, eventData, ExecuteEvents.submitHandler);
            return true;
        }

        private static bool IsValidSubmitTarget(GameObject target)
        {
            if (target == null || !target.activeInHierarchy)
            {
                return false;
            }

            var selectable = UiFocus.SelectableOf(target);
            return selectable == null || selectable.IsInteractable();
        }

        private void OnPointPerformed(InputAction.CallbackContext context)
        {
            if (context.control?.device is not Mouse)
            {
                return;
            }

            if (UiInputModality.IsMouse)
            {
                return;
            }

            var position = context.ReadValue<Vector2>();
            if (!_hasPointerAnchor)
            {
                _pointerAnchor = position;
                _hasPointerAnchor = true;
                return;
            }

            if ((position - _pointerAnchor).sqrMagnitude < mouseActivationDistance * mouseActivationDistance)
            {
                return;
            }

            SetModality(UiInputMode.Mouse);
        }

        private void OnClickPerformed(InputAction.CallbackContext context)
        {
            if (context.control?.device is Mouse && context.ReadValueAsButton())
            {
                SetModality(UiInputMode.Mouse);
            }
        }

        private void DisableCompetingInputOwners(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                DisableCompetingComponents(roots[rootIndex]);
            }
        }

        private void RegisterFocusCoordinator(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            _focusCoordinator = null;
            _vehicleSelectionNavigation = null;
            _tabNavigation = null;
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var coordinators = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (var index = 0; index < coordinators.Length; index++)
                {
                    if (_focusCoordinator == null && coordinators[index] is IUiFocusCoordinator coordinator)
                    {
                        _focusCoordinator = coordinator;
                    }

                    if (_vehicleSelectionNavigation == null && coordinators[index] is IUiVehicleSelectionNavigation vehicleSelectionNavigation)
                    {
                        _vehicleSelectionNavigation = vehicleSelectionNavigation;
                    }

                    if (_tabNavigation == null && coordinators[index] is IUiTabNavigation tabNavigation)
                    {
                        _tabNavigation = tabNavigation;
                    }
                }
            }
        }

        private void DisableCompetingComponents(GameObject root)
        {
            var systems = root.GetComponentsInChildren<EventSystem>(true);
            for (var index = 0; index < systems.Length; index++)
            {
                if (systems[index] != null && systems[index] != eventSystem)
                {
                    systems[index].gameObject.SetActive(false);
                }
            }

            var modules = root.GetComponentsInChildren<InputSystemUIInputModule>(true);
            for (var index = 0; index < modules.Length; index++)
            {
                if (modules[index] != null && modules[index] != inputModule)
                {
                    modules[index].enabled = false;
                }
            }
        }
    }
}
