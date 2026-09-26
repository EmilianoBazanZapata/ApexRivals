using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Camera.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class WheelVehicleFollowCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField]
        [Tooltip("Experimental wheel vehicle CameraTarget used as the camera follow and look point.")]
        private Transform cameraTarget;

        [SerializeField]
        [Tooltip("Experimental wheel vehicle Rigidbody used only to read planar travel direction. Assign explicitly; no scene-wide camera or vehicle searches are performed.")]
        private Rigidbody vehicleRigidbody;

        [SerializeField]
        [Tooltip("Optional DrivingInput provider used only for the read-only reverse-input diagnostic. Reverse camera activation is based on actual Rigidbody motion, never input intent.")]
        private MonoBehaviour inputProviderComponent;

        [Header("Follow")]
        [SerializeField]
        [Tooltip("Distance the camera trails behind the vehicle's effective travel direction. Higher values frame more of the road; lower values keep the camera closer.")]
        [Min(0f)]
        private float followDistance = 5f;

        [SerializeField]
        [Tooltip("World-space height added above the CameraTarget. Higher values create a more elevated chase view; lower values place the camera nearer to chassis height.")]
        private float followHeight = 2f;

        [SerializeField]
        [Tooltip("Controls how quickly the camera moves toward its desired follow position. Higher values feel tighter; lower values create more positional lag during drift.")]
        [Min(0f)]
        private float positionFollowSpeed = 5f;

        [Header("Direction")]
        [SerializeField]
        [Tooltip("Controls how strongly actual planar vehicle velocity affects the camera follow direction. 0 follows chassis heading only; higher values make sideways drift more visible by following trajectory.")]
        [Min(0f)]
        private float velocityDirectionInfluence = 1f;

        [SerializeField]
        [Tooltip("Minimum planar vehicle speed required before velocity contributes to camera direction. Higher values use chassis heading for longer at low speed; lower values react to slower movement. Measured in m/s.")]
        [Min(0f)]
        private float velocityDirectionMinimumSpeed = 0.75f;

        [Header("Reverse Camera")]
        [SerializeField]
        [Tooltip("Distance the camera moves in front of the vehicle while reversing. Higher values frame more of the road ahead of the chassis; lower values keep the reverse view closer.")]
        [Min(0f)]
        private float reverseFollowDistance = 5f;

        [SerializeField]
        [Tooltip("World-space height above CameraTarget while using the reverse front view. Higher values create a more elevated reverse framing; lower values place the camera nearer to chassis height.")]
        private float reverseFollowHeight = 2f;

        [SerializeField]
        [Tooltip("Signed forward speed at which the reverse camera activates. The vehicle must be physically moving backward at or beyond this speed. Measured in m/s.")]
        [Min(0f)]
        private float reverseCameraEnterSpeed = 0.75f;

        [SerializeField]
        [Tooltip("Signed forward speed threshold for returning from the reverse camera. Keep this lower than Reverse Camera Enter Speed to prevent switching around zero. Measured in m/s.")]
        [Min(0f)]
        private float reverseCameraExitSpeed = 0.25f;

        [Header("Look")]
        [SerializeField]
        [Tooltip("Additional world-space height above CameraTarget used as the look point. Higher values frame more of the vehicle roof and road ahead; lower values aim nearer to the target center.")]
        private float lookHeightOffset;

        [SerializeField]
        [Tooltip("Controls how quickly the camera rotates toward its look target. Higher values react faster; lower values allow more visual separation between chassis heading and travel direction during drift.")]
        [Min(0f)]
        private float rotationFollowSpeed = 10f;

        [Header("Lens")]
        [SerializeField]
        [Tooltip("Normal camera field of view in degrees. Higher values show more peripheral motion; lower values create a narrower view.")]
        [Range(1f, 179f)]
        private float baseFieldOfView = 60f;

        [SerializeField]
        [Tooltip("Additional field of view added at maximum configured speed. 0 preserves the reference-like fixed FOV; higher values add a subtle sense of speed.")]
        [Min(0f)]
        private float speedFovIncrease;

        [SerializeField]
        [Tooltip("Planar speed at which Speed FOV Increase reaches its full value. Higher values make FOV expansion build more gradually. Measured in m/s.")]
        [Min(0.01f)]
        private float speedForMaximumFov = 50f;

        private UnityEngine.Camera _camera;
        private bool _isReverseCamera;

        public float CurrentPlanarSpeed { get; private set; }
        public float CurrentSignedForwardSpeed { get; private set; }
        public bool HasInputProvider => inputProviderComponent is IDrivingInputProvider;
        public bool ReverseInputActive { get; private set; }
        public bool ReverseCameraActive => _isReverseCamera;
        public Vector3 CurrentVehicleForwardDirection { get; private set; }
        public Vector3 CurrentVelocityDirection { get; private set; }
        public Vector3 CurrentEffectiveFollowDirection { get; private set; }
        public Vector3 CurrentDesiredCameraPosition { get; private set; }
        public Vector3 CurrentCameraPosition => transform.position;
        public float CurrentCameraDistance { get; private set; }
        public float CurrentDistanceFromDesiredPosition { get; private set; }
        public float CurrentDesiredCameraSideDot { get; private set; }
        public float CurrentCameraSideDot { get; private set; }
        public float CurrentFieldOfView { get; private set; }

        public void SetTarget(Transform target)
        {
            cameraTarget = target;
        }

        public void SetVehicleRigidbody(Rigidbody targetRigidbody)
        {
            vehicleRigidbody = targetRigidbody;
        }

        public void SetInputProvider(IDrivingInputProvider inputProvider)
        {
            inputProviderComponent = inputProvider as MonoBehaviour;
        }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
        }

        private void LateUpdate()
        {
            if (cameraTarget == null || vehicleRigidbody == null || _camera == null)
            {
                return;
            }

            UpdateDirections();
            UpdateReverseCameraState();
            UpdatePosition();
            UpdateRotation();
            UpdateFieldOfView();
        }

        private void UpdateDirections()
        {
            var planarForward = Vector3.ProjectOnPlane(vehicleRigidbody.transform.forward, Vector3.up);
            CurrentVehicleForwardDirection = planarForward.sqrMagnitude > 0.0001f
                ? planarForward.normalized
                : Vector3.forward;

            var planarVelocity = Vector3.ProjectOnPlane(vehicleRigidbody.linearVelocity, Vector3.up);
            CurrentPlanarSpeed = planarVelocity.magnitude;
            CurrentSignedForwardSpeed = Vector3.Dot(planarVelocity, CurrentVehicleForwardDirection);
            CurrentVelocityDirection = CurrentPlanarSpeed >= Mathf.Max(0f, velocityDirectionMinimumSpeed)
                ? planarVelocity / CurrentPlanarSpeed
                : Vector3.zero;

            var blendedDirection = CurrentVehicleForwardDirection
                + CurrentVelocityDirection * Mathf.Max(0f, velocityDirectionInfluence);
            CurrentEffectiveFollowDirection = blendedDirection.sqrMagnitude > 0.0001f
                ? blendedDirection.normalized
                : CurrentVelocityDirection.sqrMagnitude > 0.0001f
                    ? CurrentVelocityDirection
                    : CurrentVehicleForwardDirection;
        }

        private void UpdateReverseCameraState()
        {
            var inputProvider = inputProviderComponent as IDrivingInputProvider;
            ReverseInputActive = inputProvider != null && inputProvider.CurrentInput.Brake > 0f;

            var enterSpeed = Mathf.Max(0f, reverseCameraEnterSpeed);
            var exitSpeed = Mathf.Clamp(reverseCameraExitSpeed, 0f, enterSpeed);
            if (_isReverseCamera)
            {
                _isReverseCamera = CurrentSignedForwardSpeed < -exitSpeed;
                return;
            }

            _isReverseCamera = CurrentSignedForwardSpeed <= -enterSpeed;
        }

        private void UpdatePosition()
        {
            CurrentDesiredCameraPosition = CalculateDesiredCameraPosition();
            var followAmount = CalculateFollowAmount(positionFollowSpeed);
            transform.position = Vector3.Lerp(transform.position, CurrentDesiredCameraPosition, followAmount);
            CurrentCameraDistance = Vector3.Distance(transform.position, cameraTarget.position);
            CurrentDistanceFromDesiredPosition = Vector3.Distance(transform.position, CurrentDesiredCameraPosition);
            CurrentDesiredCameraSideDot = Vector3.Dot(
                CurrentDesiredCameraPosition - cameraTarget.position,
                CurrentVehicleForwardDirection);
            CurrentCameraSideDot = Vector3.Dot(
                transform.position - cameraTarget.position,
                CurrentVehicleForwardDirection);
        }

        private Vector3 CalculateDesiredCameraPosition()
        {
            if (_isReverseCamera)
            {
                return cameraTarget.position
                    + CurrentVehicleForwardDirection * Mathf.Max(0f, reverseFollowDistance)
                    + Vector3.up * reverseFollowHeight;
            }

            return cameraTarget.position
                + Vector3.up * followHeight
                - CurrentEffectiveFollowDirection * Mathf.Max(0f, followDistance);
        }

        private void UpdateRotation()
        {
            var lookTarget = cameraTarget.position + Vector3.up * lookHeightOffset;
            var lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                CalculateFollowAmount(rotationFollowSpeed));
        }

        private void UpdateFieldOfView()
        {
            var speedFraction = CurrentPlanarSpeed / Mathf.Max(0.01f, speedForMaximumFov);
            CurrentFieldOfView = Mathf.Clamp(baseFieldOfView + speedFovIncrease * Mathf.Clamp01(speedFraction), 1f, 179f);
            _camera.fieldOfView = CurrentFieldOfView;
        }

        private static float CalculateFollowAmount(float followSpeed)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, followSpeed) * Time.deltaTime);
        }
    }
}
