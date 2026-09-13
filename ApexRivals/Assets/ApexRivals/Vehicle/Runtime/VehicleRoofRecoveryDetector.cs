using ApexRivals.Input.Runtime;
using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>
    /// Player-requested roof recovery eligibility. It only detects an inverted roof
    /// resting on a surface; VehicleResetter remains the reset authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleRoofRecoveryDetector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody vehicleRigidbody;
        [SerializeField] private Transform roofRecoveryRayOrigin;
        [SerializeField] private DrivingInputGate drivingInputGate;
        [SerializeField] private WheelArcadeVehicleController vehicleController;

        [Header("Detection")]
        [SerializeField, Range(-1f, -0.1f)] private float invertedDotThreshold = -0.5f;
        [SerializeField, Min(0.05f)] private float roofRayDistance = 0.75f;
        [SerializeField, Min(0f)] private float invertedDebounceDuration = 0.2f;
        [SerializeField] private LayerMask recoverySurfaceLayers = Physics.DefaultRaycastLayers;

        [Header("Read-only diagnostics")]
        [SerializeField] private float upDot;
        [SerializeField] private bool isInverted;
        [SerializeField] private bool roofRayHit;
        [SerializeField] private float roofHitDistance = -1f;
        [SerializeField] private bool canRecoverVehicle;
        [SerializeField] private RecoveryInputSource lastRecoveryInput;

        private float _invertedTime;
        // Fixed-size and reused: roof recovery never allocates while sampling.
        private readonly RaycastHit[] _roofRayHits = new RaycastHit[8];

        public float UpDot => upDot;
        public bool IsInverted => isInverted;
        public bool RoofRayHit => roofRayHit;
        public float RoofHitDistance => roofHitDistance;
        public bool CanRecoverVehicle => canRecoverVehicle;
        public RecoveryInputSource LastRecoveryInput => lastRecoveryInput;

        private void Awake()
        {
            if (vehicleRigidbody == null)
            {
                vehicleRigidbody = GetComponent<Rigidbody>();
            }

            if (drivingInputGate == null)
            {
                drivingInputGate = GetComponent<DrivingInputGate>();
            }

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
                var hitCount = Physics.RaycastNonAlloc(
                    origin,
                    direction,
                    _roofRayHits,
                    roofRayDistance,
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
            canRecoverVehicle = VehicleRoofRecoveryRules.CanRecover(
                isInverted,
                roofRayHit,
                _invertedTime,
                invertedDebounceDuration);
        }

        private void TryConsumeRecoveryInput()
        {
            if (drivingInputGate == null)
            {
                return;
            }

            var input = drivingInputGate.CurrentInput;
            if (!input.RecoverVehicleRequested)
            {
                return;
            }

            // Consume even when unavailable, so a press made while airborne cannot
            // become a delayed reset after the car lands inverted.
            drivingInputGate.ConsumeRecoveryRequest();
            lastRecoveryInput = input.RecoveryRequestSource;
            if (!canRecoverVehicle || vehicleController == null)
            {
                return;
            }

            if (vehicleController.TryResetVehicle())
            {
                _invertedTime = 0f;
                canRecoverVehicle = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            var origin = roofRecoveryRayOrigin != null ? roofRecoveryRayOrigin.position : transform.position;
            var direction = roofRecoveryRayOrigin != null ? roofRecoveryRayOrigin.up : transform.up;
            Gizmos.color = roofRayHit && isInverted ? Color.green : Color.red;
            Gizmos.DrawLine(origin, origin + direction * roofRayDistance);
            Gizmos.DrawWireSphere(origin, 0.06f);
            if (roofRayHit)
            {
                Gizmos.DrawWireSphere(origin + direction * roofHitDistance, 0.09f);
            }
        }

        public bool TryValidateConfiguration(out string message)
        {
            if (vehicleRigidbody == null || roofRecoveryRayOrigin == null || drivingInputGate == null || vehicleController == null)
            {
                message = $"VehicleRoofRecoveryDetector on '{gameObject.name}' requires Rigidbody, RoofRecoveryRayOrigin, DrivingInputGate, and WheelArcadeVehicleController.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
