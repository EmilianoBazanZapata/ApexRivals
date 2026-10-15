using UnityEngine;
using UnityEngine.SceneManagement;

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
        private BoxCollider[] _bodyColliders;
        private readonly Collider[] _overlaps = new Collider[16];

        private void Awake()
        {
            if (vehicleRigidbody == null)
            {
                vehicleRigidbody = GetComponent<Rigidbody>();
            }

            CaptureResetPose(initialResetPose != null ? initialResetPose : transform);
            _bodyColliders = GetComponentsInChildren<BoxCollider>(true);
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
            vehicleRigidbody.transform.SetPositionAndRotation(_latestValidPosition, _latestValidRotation);
            vehicleRigidbody.linearVelocity = Vector3.zero;
            vehicleRigidbody.angularVelocity = Vector3.zero;
            vehicleRigidbody.Sleep();
            vehicleRigidbody.WakeUp();
            Physics.SyncTransforms();
            vehicleRigidbody.interpolation = interpolation;
        }

        public bool TryCaptureSafeResetPose(Vector3 position, Quaternion rotation)
        {
            if (vehicleRigidbody == null || !IsPoseClear(position, rotation)) return false;
            CaptureResetPose(position, rotation);
            return true;
        }

        public bool TryResetVehicle()
        {
            if (vehicleRigidbody == null || !_hasResetPose || !IsPoseClear(_latestValidPosition, _latestValidRotation)) return false;
            ResetVehicle();
            return true;
        }

        private bool IsPoseClear(Vector3 position, Quaternion rotation)
        {
            if (_bodyColliders == null) _bodyColliders = GetComponentsInChildren<BoxCollider>(true);
            var rotationDelta = rotation * Quaternion.Inverse(transform.rotation);
            foreach (var box in _bodyColliders)
            {
                if (box == null || !box.enabled || box.isTrigger || !box.gameObject.activeInHierarchy) continue;
                var center = position + rotationDelta * (box.transform.TransformPoint(box.center) - transform.position);
                var scale = box.transform.lossyScale;
                var halfExtents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                var count = gameObject.scene.GetPhysicsScene().OverlapBox(center, halfExtents, _overlaps,
                    rotationDelta * box.transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (count == _overlaps.Length) return false;
                for (var i = 0; i < count; i++)
                {
                    var other = _overlaps[i];
                    if (other == null || other.attachedRigidbody == vehicleRigidbody || other.transform.IsChildOf(transform)) continue;
                    if (Physics.GetIgnoreLayerCollision(box.gameObject.layer, other.gameObject.layer) || Physics.GetIgnoreCollision(box, other)) continue;
                    return false;
                }
            }

            return true;
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
