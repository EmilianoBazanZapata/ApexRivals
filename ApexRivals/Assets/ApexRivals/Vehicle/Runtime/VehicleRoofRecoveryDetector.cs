using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Player-requested recovery for a resting inverted roof or a car outside the
    /// road corridor; VehicleResetter remains the reset authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleRoofRecoveryDetector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody vehicleRigidbody;
        [SerializeField] private Transform roofRecoveryRayOrigin;
        // Keep the serialized field name and object reference used by existing vehicles.
        [SerializeField] private MonoBehaviour drivingInputGate;
        [SerializeField] private WheelArcadeVehicleController vehicleController;

        [Header("Detection")]
        [SerializeField, Range(-1f, -0.1f)] private float invertedDotThreshold = -0.5f;
        [SerializeField, Min(0.05f)] private float roofRayDistance = 0.75f;
        [SerializeField, Min(0f)] private float invertedDebounceDuration = 0.2f;
        [SerializeField, Min(0f)] private float outsideTrackDebounceDuration = 0.6f;
        [SerializeField] private LayerMask recoverySurfaceLayers = Physics.DefaultRaycastLayers;

        [Header("Read-only diagnostics")]
        [SerializeField] private float upDot;
        [SerializeField] private bool isInverted;
        [SerializeField] private bool roofRayHit;
        [SerializeField] private float roofHitDistance = -1f;
        [SerializeField] private bool canRecoverVehicle;
        [SerializeField] private bool isOutsideTrack;
        [SerializeField] private RecoveryInputSource lastRecoveryInput;

        private float _invertedTime;
        private float _outsideTrackTime;
        private IVehicleRecoveryAvailability _recoveryAvailability;
        private IDrivingInputProvider _recoveryInput;
        // Fixed-size and reused: roof recovery never allocates while sampling.
        private readonly RaycastHit[] _roofRayHits = new RaycastHit[8];

        public float UpDot => upDot;
        public bool IsInverted => isInverted;
        public bool RoofRayHit => roofRayHit;
        public float RoofHitDistance => roofHitDistance;
        public bool CanRecoverVehicle => canRecoverVehicle;
        public bool IsOutsideTrack => isOutsideTrack;
        public RecoveryInputSource LastRecoveryInput => lastRecoveryInput;

        public event Action<bool> RecoveryAvailabilityChanged;
        public event Action<RecoveryInputSource> RecoveryRequested;
        public event Action<RecoveryInputSource> RecoveryCompleted;

        private void Awake()
        {
            if (vehicleRigidbody == null)
            {
                vehicleRigidbody = GetComponent<Rigidbody>();
            }

            _recoveryInput = drivingInputGate as IDrivingInputProvider;

            if (vehicleController == null)
            {
                vehicleController = GetComponent<WheelArcadeVehicleController>();
            }
        }

        private void Update()
        {
            EvaluateRecoveryState();
            TryConsumeRecoveryInput();
        }

        private void EvaluateRecoveryState()
        {
            upDot = Vector3.Dot(transform.up, Vector3.up);
            isInverted = VehicleRoofRecoveryRules.IsInverted(upDot, invertedDotThreshold);

            roofRayHit = false;
            roofHitDistance = -1f;
            if (isInverted)
            {
                var direction = roofRecoveryRayOrigin != null ? roofRecoveryRayOrigin.up : transform.up;
                var origin = roofRecoveryRayOrigin != null ? roofRecoveryRayOrigin.position : transform.position;
                // The visual roof probe can extend past the chassis collider (and
                // below the road when inverted). Start inside the car and cast
                // through the roof, keeping the original outward reach. Self hits
                // are filtered below, so an upside-down resting car is still found.
                origin -= direction * roofRayDistance;
                var hitCount = gameObject.scene.GetPhysicsScene().Raycast(
                    origin,
                    direction,
                    _roofRayHits,
                    roofRayDistance * 2,
                    recoverySurfaceLayers,
                    QueryTriggerInteraction.Ignore);

                // Vehicle and track currently share the default layer. Exclude the
                // detector's hierarchy without allocations so a roof/body/wheel
                // collider can never make recovery available by itself.
                var vehicleRoot = transform.root;
                for (var index = 0; index < hitCount; index++)
                {
                    var collider = _roofRayHits[index].collider;
                    if (collider == null || collider.transform.root == vehicleRoot)
                    {
                        continue;
                    }

                    var distance = _roofRayHits[index].distance;
                    if (!roofRayHit || distance < roofHitDistance)
                    {
                        roofRayHit = true;
                        roofHitDistance = distance;
                    }
                }
            }

            _invertedTime = isInverted && roofRayHit
                ? _invertedTime + Time.deltaTime
                : 0f;
            if (_recoveryAvailability is UnityEngine.Object owner && owner == null) _recoveryAvailability = null;
            isOutsideTrack = _recoveryAvailability != null && _recoveryAvailability.IsOutsideTrack;
            _outsideTrackTime = isOutsideTrack ? _outsideTrackTime + Time.deltaTime : 0;
            SetRecoveryAvailability(VehicleRoofRecoveryRules.CanRecover(
                isInverted,
                roofRayHit,
                _invertedTime,
                invertedDebounceDuration) || VehicleRoofRecoveryRules.CanRecoverOutsideTrack(
                isOutsideTrack, _outsideTrackTime, outsideTrackDebounceDuration));
        }

        private void TryConsumeRecoveryInput()
        {
            if (_recoveryInput == null
                || (_recoveryInput is UnityEngine.Object inputObject && inputObject == null))
            {
                return;
            }

            var input = _recoveryInput.CurrentInput;
            if (!input.RecoverVehicleRequested)
            {
                return;
            }

            // Consume even when unavailable, so a press made while airborne cannot
            // become a delayed reset after the car lands inverted.
            _recoveryInput.ConsumeRecoveryRequest();
            lastRecoveryInput = input.RecoveryRequestSource;
            if (!canRecoverVehicle || vehicleController == null)
            {
                return;
            }

            RecoveryRequested?.Invoke(lastRecoveryInput);
            if (vehicleController.TryResetVehicle())
            {
                _invertedTime = 0f;
                _outsideTrackTime = 0f;
                isOutsideTrack = false;
                SetRecoveryAvailability(false);
                RecoveryCompleted?.Invoke(lastRecoveryInput);
            }
        }

        private void SetRecoveryAvailability(bool isAvailable)
        {
            if (canRecoverVehicle == isAvailable)
            {
                return;
            }

            canRecoverVehicle = isAvailable;
            RecoveryAvailabilityChanged?.Invoke(canRecoverVehicle);
        }

        private void OnDrawGizmosSelected()
        {
            var origin = roofRecoveryRayOrigin != null ? roofRecoveryRayOrigin.position : transform.position;
            var direction = roofRecoveryRayOrigin != null ? roofRecoveryRayOrigin.up : transform.up;
            origin -= direction * roofRayDistance;
            Gizmos.color = roofRayHit && isInverted ? Color.green : Color.red;
            Gizmos.DrawLine(origin, origin + direction * roofRayDistance * 2);
            Gizmos.DrawWireSphere(origin, 0.06f);
            if (roofRayHit)
            {
                Gizmos.DrawWireSphere(origin + direction * roofHitDistance, 0.09f);
            }
        }

        public bool TryValidateConfiguration(out string message)
        {
            if (vehicleRigidbody == null || roofRecoveryRayOrigin == null
                || !(drivingInputGate is IDrivingInputProvider) || drivingInputGate == null || vehicleController == null)
            {
                message = $"VehicleRoofRecoveryDetector on '{gameObject.name}' requires Rigidbody, RoofRecoveryRayOrigin, DrivingInputGate, and WheelArcadeVehicleController.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public void ConfigureRecoveryInput(IDrivingInputProvider inputProvider)
        {
            _recoveryInput = inputProvider;
            drivingInputGate = inputProvider as MonoBehaviour;
        }

        public void ConfigureRecoveryAvailability(IVehicleRecoveryAvailability availability)
        {
            _recoveryAvailability = availability;
            _outsideTrackTime = 0;
        }

        internal void ConfigureRecoveryInputIfMissing(IDrivingInputProvider inputProvider)
        {
            if (drivingInputGate == null && _recoveryInput == null)
                ConfigureRecoveryInput(inputProvider);
        }
    }
}
