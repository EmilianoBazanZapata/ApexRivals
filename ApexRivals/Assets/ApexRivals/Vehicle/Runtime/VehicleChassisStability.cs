using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>Low centre of mass and bounded roll damping while supported by the road.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleChassisStability : MonoBehaviour
    {
        [SerializeField] private Transform centerOfMassReference;
        [SerializeField, Min(0)] private float rollSpringAcceleration = 18;
        [SerializeField, Min(0)] private float rollDamping = 7;
        [SerializeField, Min(0)] private float maximumRollAcceleration = 18;
        private Rigidbody _body;
        private WheelCollider[] _wheels;
        private bool _centerOfMassApplied;
        public float CurrentRollAcceleration { get; private set; }
        public int SupportedWheelCount { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _wheels = GetComponentsInChildren<WheelCollider>();
        }

        private void OnEnable() => _centerOfMassApplied = false;

        private void FixedUpdate()
        {
            CurrentRollAcceleration = 0;
            SupportedWheelCount = 0;
            if (_body == null || _body.isKinematic || _wheels == null) return;
            // Apply after controller Awake has finished resetting the automatic mass.
            if (!_centerOfMassApplied && centerOfMassReference != null)
            {
                _body.centerOfMass = transform.InverseTransformPoint(centerOfMassReference.position);
                _centerOfMassApplied = true;
            }
            var normal = Vector3.zero;
            var rotationDelta = _body.rotation * Quaternion.Inverse(transform.rotation);
            foreach (var wheel in _wheels)
            {
                if (wheel == null || !wheel.enabled || !wheel.GetGroundHit(out var hit) || hit.collider == null ||
                    hit.collider.attachedRigidbody == _body || hit.normal.y < .5f) continue;
                // Reject cached wheel contacts after a teleport/launch.
                var center = _body.position + rotationDelta * (wheel.transform.TransformPoint(wheel.center) - transform.position);
                if (Vector3.Distance(center, hit.point) >
                    wheel.radius + wheel.suspensionDistance + .15f) continue;
                normal += hit.normal;
                SupportedWheelCount++;
            }
            if (SupportedWheelCount < 2 || normal.sqrMagnitude < .001f) return;
            normal.Normalize();
            var up = _body.rotation * Vector3.up;
            if (Vector3.Dot(up, normal) < .5f) return; // No automatic self-righting after a rollover.
            var forward = _body.rotation * Vector3.forward;
            var right = _body.rotation * Vector3.right;
            var rollError = Mathf.Asin(Mathf.Clamp(Vector3.Dot(right, normal), -1, 1));
            var rollSpeed = Vector3.Dot(_body.angularVelocity, forward);
            CurrentRollAcceleration = Mathf.Clamp(-rollError * rollSpringAcceleration - rollSpeed * rollDamping,
                -maximumRollAcceleration, maximumRollAcceleration);
            // Roll only: no yaw correction, vertical lift, frozen rotations or air steering.
            _body.AddTorque(forward * CurrentRollAcceleration, ForceMode.Acceleration);
        }
    }
}
