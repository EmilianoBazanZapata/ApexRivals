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
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private float moveRepeatDelay = 0.45f;
        [SerializeField] private float moveRepeatRate = 0.12f;

        private readonly List<InputActionReference> _runtimeReferences = new List<InputActionReference>();
        private InputAction _navigateAction;
        private InputAction _submitAction;
        private InputAction _cancelAction;
        private InputAction _pointAction;
        private InputAction _clickAction;
        private InputAction _previousVehicleAction;
        private InputAction _nextVehicleAction;
        private IUiFocusCoordinator _focusCoordinator;
        private IUiVehicleSelectionNavigation _vehicleSelectionNavigation;
        private Vector2 _lastPointerPosition;
        private double _lastSubmitTime = double.NegativeInfinity;
        private double _lastCancelTime = double.NegativeInfinity;
        private Coroutine _deferredSubmit;
        private bool _hasPointerPosition;

        private void Awake()
        {
            EnsureComponents();
            ConfigureInputModule();
            DisableCompetingInputOwners(gameObject.scene);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
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

            inputModule.actionsAsset = inputActions;
            inputModule.moveRepeatDelay = moveRepeatDelay;
            inputModule.moveRepeatRate = moveRepeatRate;
            ClearRuntimeReferences();
            inputModule.move = CreateReference(uiMap, "Navigate");
            inputModule.submit = null;
            inputModule.cancel = null;
            inputModule.point = CreateReference(uiMap, "Point");
            inputModule.leftClick = CreateReference(uiMap, "Click");
            inputModule.rightClick = CreateReference(uiMap, "RightClick");
            inputModule.middleClick = CreateReference(uiMap, "MiddleClick");
            inputModule.scrollWheel = CreateReference(uiMap, "ScrollWheel");

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
            _navigateAction = uiMap.FindAction("Navigate", false);
            _submitAction = uiMap.FindAction("Submit", false);
            _cancelAction = uiMap.FindAction("Cancel", false);
            _pointAction = uiMap.FindAction("Point", false);
            _clickAction = uiMap.FindAction("Click", false);
            _previousVehicleAction = uiMap.FindAction("PreviousVehicle", false);
            _nextVehicleAction = uiMap.FindAction("NextVehicle", false);

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

            if (_previousVehicleAction != null)
            {
                _previousVehicleAction.performed += OnPreviousVehiclePerformed;
            }

            if (_nextVehicleAction != null)
            {
                _nextVehicleAction.performed += OnNextVehiclePerformed;
            }
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

            if (_previousVehicleAction != null)
            {
                _previousVehicleAction.performed -= OnPreviousVehiclePerformed;
            }

            if (_nextVehicleAction != null)
            {
                _nextVehicleAction.performed -= OnNextVehiclePerformed;
            }

            _navigateAction = null;
            _submitAction = null;
            _cancelAction = null;
            _pointAction = null;
            _clickAction = null;
            _previousVehicleAction = null;
            _nextVehicleAction = null;
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValue<Vector2>().sqrMagnitude < 0.25f)
            {
                return;
            }

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
                Debug.Log($"[UI_INPUT] Submit ignored as duplicate. control={context.control?.path} selected={SelectedName()} time={context.time:0.000}");
                return;
            }

            Debug.Log($"[UI_INPUT] Submit received. control={context.control?.path} selectedBefore={SelectedName()} time={context.time:0.000}");
            _focusCoordinator?.NotifyNavigationInput();
            if (TrySubmitCurrentSelection())
            {
                Debug.Log($"[UI_INPUT] Submit executed immediately. selected={SelectedName()}");
                return;
            }

            if (_deferredSubmit != null)
            {
                StopCoroutine(_deferredSubmit);
            }

            Debug.Log($"[UI_INPUT] Submit deferred because current selection is invalid. selected={SelectedName()}");
            _deferredSubmit = StartCoroutine(SubmitAfterSelectionRepair());
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (!context.ReadValueAsButton())
            {
                return;
            }

            if (IsDuplicateCancel(context.time))
            {
                Debug.Log($"[UI_INPUT] Cancel ignored as duplicate. control={context.control?.path} selected={SelectedName()} time={context.time:0.000}");
                return;
            }

            Debug.Log($"[UI_INPUT] Cancel received. control={context.control?.path} selectedBefore={SelectedName()} time={context.time:0.000}");
            _focusCoordinator?.NotifyNavigationInput();
            _focusCoordinator?.NotifyCancelInput();
            Debug.Log($"[UI_INPUT] Cancel processed. selectedAfter={SelectedName()}");
        }

        private void OnPreviousVehiclePerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValueAsButton())
            {
                _vehicleSelectionNavigation?.SelectPreviousVehicle();
            }
        }

        private void OnNextVehiclePerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValueAsButton())
            {
                _vehicleSelectionNavigation?.SelectNextVehicle();
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

        private System.Collections.IEnumerator SubmitAfterSelectionRepair()
        {
            yield return null;

            _focusCoordinator?.NotifyNavigationInput();
            var submitted = TrySubmitCurrentSelection();
            Debug.Log($"[UI_INPUT] Deferred submit processed. submitted={submitted} selected={SelectedName()}");
            _deferredSubmit = null;
        }

        private static bool TrySubmitCurrentSelection()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var selected = ExecuteEvents.GetEventHandler<ISubmitHandler>(EventSystem.current.currentSelectedGameObject);
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

            var selectable = target.GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }

        private static string SelectedName()
        {
            return EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.name
                : "null";
        }

        private void OnPointPerformed(InputAction.CallbackContext context)
        {
            if (context.control?.device is not Mouse)
            {
                return;
            }

            var position = context.ReadValue<Vector2>();
            if (_hasPointerPosition && (position - _lastPointerPosition).sqrMagnitude < 0.01f)
            {
                return;
            }

            _lastPointerPosition = position;
            _hasPointerPosition = true;
            _focusCoordinator?.NotifyMouseInput();
        }

        private void OnClickPerformed(InputAction.CallbackContext context)
        {
            if (context.control?.device is Mouse && context.ReadValueAsButton())
            {
                _focusCoordinator?.NotifyMouseInput();
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
