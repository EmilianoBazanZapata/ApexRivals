using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VehicleResetter : MonoBehaviour
    {
        [SerializeField]
        private Rigidbody vehicleRigidbody;

        [SerializeField]
        private Transform initialResetPose;

        private Vector3 _latestValidPosition;
        private Quaternion _latestValidRotation;
        private bool _hasResetPose;

        private void Awake()
        {
            if (vehicleRigidbody == null)
            {
                vehicleRigidbody = GetComponent<Rigidbody>();
            }

            CaptureResetPose(initialResetPose != null ? initialResetPose : transform);
        }

        public void CaptureResetPose(Transform resetPose)
        {
            if (resetPose == null)
            {
                return;
            }

            CaptureResetPose(resetPose.position, resetPose.rotation);
        }

        public void CaptureResetPose(Vector3 position, Quaternion rotation)
        {
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            _latestValidPosition = position;
            _latestValidRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            _hasResetPose = true;
        }

        public void ResetVehicle()
        {
            if (vehicleRigidbody == null || !_hasResetPose)
            {
                return;
            }

            // Clear the interpolation history around a teleport. Otherwise an interpolated
            // Rigidbody may render one or more frames between the pre-reset crash pose and
            // the reset pose.
            var interpolation = vehicleRigidbody.interpolation;
            vehicleRigidbody.interpolation = RigidbodyInterpolation.None;
            vehicleRigidbody.position = _latestValidPosition;
            vehicleRigidbody.rotation = _latestValidRotation;
            vehicleRigidbody.linearVelocity = Vector3.zero;
            vehicleRigidbody.angularVelocity = Vector3.zero;
            vehicleRigidbody.Sleep();
            vehicleRigidbody.WakeUp();
            Physics.SyncTransforms();
            vehicleRigidbody.interpolation = interpolation;
        }

        public bool TryValidateConfiguration(out string message)
        {
            if (vehicleRigidbody == null)
            {
                message = $"VehicleResetter on '{gameObject.name}' requires a Rigidbody.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
