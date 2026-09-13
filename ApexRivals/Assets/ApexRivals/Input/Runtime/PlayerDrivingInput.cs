using ApexRivals.Vehicle.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ApexRivals.Input.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerDrivingInput : MonoBehaviour, IDrivingInputProvider
    {
        [SerializeField, Range(0f, 0.5f)]
        private float steeringDeadZone = 0.08f;

        private InputActionMap _drivingActions;
        private InputAction _throttleAction;
        private InputAction _brakeAction;
        private InputAction _steeringAction;
        private InputAction _driftAction;
        private InputAction _resetAction;
        private InputAction _recoverVehicleAction;
        private DrivingInput _currentInput;
        private bool _resetRequested;
        private RecoveryInputSource _recoveryInputSource;
        private bool _isSubscribed;

        public DrivingInput CurrentInput => new DrivingInput(
            _currentInput.Throttle,
            _currentInput.Brake,
            _currentInput.Steering,
            _currentInput.Drift,
            _resetRequested,
            _recoveryInputSource);

        private void Awake()
        {
            CreateDrivingActions();
        }

        private void OnEnable()
        {
            Subscribe();
            _drivingActions.Enable();
        }

        private void Update()
        {
            var steering = _steeringAction.ReadValue<float>();
            if (Mathf.Abs(steering) < steeringDeadZone)
            {
                steering = 0f;
            }

            _currentInput = new DrivingInput(
                _throttleAction.ReadValue<float>(),
                _brakeAction.ReadValue<float>(),
                steering,
                _driftAction.IsPressed(),
                _resetRequested,
                _recoveryInputSource);
        }

        private void OnDisable()
        {
            _drivingActions.Disable();
            Unsubscribe();
            _currentInput = DrivingInput.Neutral;
            _resetRequested = false;
            _recoveryInputSource = RecoveryInputSource.None;
        }

        private void OnDestroy()
        {
            _drivingActions.Dispose();
        }

        public void ConsumeResetRequest()
        {
            _resetRequested = false;
            _currentInput = _currentInput.WithoutResetRequest();
        }

        public void ConsumeRecoveryRequest()
        {
            _recoveryInputSource = RecoveryInputSource.None;
            _currentInput = _currentInput.WithoutRecoveryRequest();
        }

        private void CreateDrivingActions()
        {
            _drivingActions = new InputActionMap("Driving");

            _throttleAction = _drivingActions.AddAction("Throttle", InputActionType.Value, expectedControlLayout: "Axis");
            _throttleAction.AddBinding("<Keyboard>/w");
            _throttleAction.AddBinding("<Keyboard>/upArrow");
            _throttleAction.AddBinding("<Gamepad>/rightTrigger");

            _brakeAction = _drivingActions.AddAction("Brake", InputActionType.Value, expectedControlLayout: "Axis");
            _brakeAction.AddBinding("<Keyboard>/s");
            _brakeAction.AddBinding("<Keyboard>/downArrow");
            _brakeAction.AddBinding("<Gamepad>/leftTrigger");

            _steeringAction = _drivingActions.AddAction("Steering", InputActionType.Value, expectedControlLayout: "Axis");
            _steeringAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            _steeringAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            _steeringAction.AddBinding("<Gamepad>/leftStick/x");

            _driftAction = _drivingActions.AddAction("Drift", InputActionType.Button, expectedControlLayout: "Button");
            _driftAction.AddBinding("<Keyboard>/space");
            _driftAction.AddBinding("<Gamepad>/buttonSouth");

            _resetAction = _drivingActions.AddAction("Reset", InputActionType.Button, expectedControlLayout: "Button");
            _resetAction.AddBinding("<Keyboard>/r");
            _resetAction.AddBinding("<Gamepad>/buttonNorth");

            _recoverVehicleAction = _drivingActions.AddAction("RecoverVehicle", InputActionType.Button, expectedControlLayout: "Button");
            _recoverVehicleAction.AddBinding("<Keyboard>/e");
            // The common Gamepad "select" control maps Xbox View (historically Back/Select),
            // not the Xbox Menu/Start button.
            _recoverVehicleAction.AddBinding("<Gamepad>/select");
        }

        private void Subscribe()
        {
            if (_isSubscribed)
            {
                return;
            }

            _resetAction.performed += OnResetPerformed;
            _recoverVehicleAction.performed += OnRecoverVehiclePerformed;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
            {
                return;
            }

            _resetAction.performed -= OnResetPerformed;
            _recoverVehicleAction.performed -= OnRecoverVehiclePerformed;
            _isSubscribed = false;
        }

        private void OnResetPerformed(InputAction.CallbackContext context)
        {
            _resetRequested = true;
        }

        private void OnRecoverVehiclePerformed(InputAction.CallbackContext context)
        {
            _recoveryInputSource = context.control.device is Gamepad
                ? RecoveryInputSource.GamepadView
                : RecoveryInputSource.KeyboardE;
        }
    }
}
