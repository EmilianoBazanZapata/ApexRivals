using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    /// <summary>Tracks safe road positions within the validated sector; never awards checkpoint progress.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCheckpointRecovery : MonoBehaviour, IVehicleRecoveryPoseProvider, IVehicleRecoveryAvailability
    {
        private RaceCoordinator _coordinator;
        private RaceParticipant _participant;
        private VehicleResetter _resetter;
        private Rigidbody _body;
        private WheelCollider[] _wheels;
        private bool _capturePending;
        private Vector3 _checkpointPosition;
        private bool _subscribed;
        private float _captureTimer;
        private bool _hasRouteAnchor;
        private Vector3 _anchorRoutePoint;
        private Quaternion _anchorRotation;
        private int _anchorPassedCheckpoints;
        private readonly RaycastHit[] _roadHits = new RaycastHit[16];
        private static readonly float[] RecoveryLanes = { 0, -2.2f, 2.2f };

        public bool IsOutsideTrack
        {
            get
            {
                if (_coordinator == null || _coordinator.State != RaceState.Racing || _body == null || _participant == null ||
                    !_coordinator.TryGetNearestTrackPoint(_participant, _body.position, out var point)) return false;
                var offset = _body.position - point;
                // Road half-width is 7 m. Allow kerbs/runoff without treating every
                // minor excursion as a recovery, and distinguish elevated crossings.
                // Eligibility checks the whole road (including the starting grid);
                // the destination remains restricted to validated checkpoint progress.
                return Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude > 100 || Mathf.Abs(offset.y) > 5;
            }
        }

        public void Configure(RaceCoordinator coordinator, RaceParticipant participant, VehicleResetter resetter)
        {
            Unsubscribe();
            _coordinator = coordinator;
            _participant = participant;
            _resetter = resetter;
            _body = GetComponent<Rigidbody>();
            _wheels = GetComponentsInChildren<WheelCollider>();
            _capturePending = false;
            _captureTimer = 0;
            _hasRouteAnchor = false;
            if (_resetter != null) _resetter.ConfigureRecoveryPoseProvider(this);
            var detector = GetComponent<VehicleRoofRecoveryDetector>();
            if (detector != null) detector.ConfigureRecoveryAvailability(this);
            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled || _coordinator == null) return;
            _coordinator.CheckpointPassed += OnCheckpointPassed;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && _coordinator != null) _coordinator.CheckpointPassed -= OnCheckpointPassed;
            _subscribed = false;
        }

        private void OnCheckpointPassed(CheckpointPassedEvent passed)
        {
            if (_participant == null || passed.RacerId != _participant.RacerId || _coordinator == null) return;
            if (passed.CheckpointIndex < 0 || passed.CheckpointIndex >= _coordinator.OrderedCheckpoints.Count) return;
            var checkpoint = _coordinator.OrderedCheckpoints[passed.CheckpointIndex];
            if (checkpoint == null) return;
            _checkpointPosition = checkpoint.Position;
            _capturePending = true;
            TryCapture();
        }

        private void FixedUpdate()
        {
            _captureTimer -= Time.fixedDeltaTime;
            if (_capturePending || (_coordinator != null && _coordinator.HasRecoveryRoute && _captureTimer <= 0))
            {
                _captureTimer = .35f;
                TryCapture();
            }
        }

        private void TryCapture()
        {
            if (_coordinator == null || _coordinator.State != RaceState.Racing || _participant == null || _resetter == null || _body == null) return;
            if (_coordinator.HasRecoveryRoute)
            {
                TryCaptureOnRoute();
                return;
            }
            // Small test/prototype tracks without an authored racing line retain the
            // checkpoint-only policy, rather than guessing a route across the map.
            // Do not let a pending airborne crossing become a new anchor far away
            // from its checkpoint. Ground contact and upright attitude are checked below.
            if (Vector3.Distance(_body.position, _checkpointPosition) > 45f)
            {
                _capturePending = false;
                return;
            }

            if (Vector3.Dot(_body.rotation * Vector3.up, Vector3.up) < 0.9f || _wheels == null || _wheels.Length == 0) return;
            var rotationDelta = _body.rotation * Quaternion.Inverse(transform.rotation);
            foreach (var wheel in _wheels)
            {
                if (wheel == null || !wheel.GetGroundHit(out var hit)) return;
                // Ground hits belong to the last simulation step. Reject a stale hit
                // after a teleport instead of recording an airborne recovery anchor.
                var wheelCenter = _body.position + rotationDelta * (wheel.transform.TransformPoint(wheel.center) - transform.position);
                if (Vector3.Distance(wheelCenter, hit.point) > wheel.radius + wheel.suspensionDistance + 0.1f) return;
            }

            var forward = Vector3.ProjectOnPlane(_body.rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) return;
            var rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            // A small lift prevents the chassis intersecting the road after its
            // suspension unloads; the reset boundary also checks body clearance.
            if (_resetter.TryCaptureSafeResetPose(_body.position + Vector3.up * 0.15f, rotation)) _capturePending = false;
        }

        private void TryCaptureOnRoute()
        {
            if (Vector3.Dot(_body.rotation * Vector3.up, Vector3.up) < .85f || _wheels == null) return;
            var rotationDelta = _body.rotation * Quaternion.Inverse(transform.rotation);
            var grounded = 0;
            foreach (var wheel in _wheels)
            {
                if (wheel == null || !wheel.enabled || !wheel.GetGroundHit(out var hit) || hit.collider == null ||
                    hit.collider.attachedRigidbody != null || hit.normal.y < .7f) continue;
                var center = _body.position + rotationDelta * (wheel.transform.TransformPoint(wheel.center) - transform.position);
                if (Vector3.Distance(center, hit.point) <= wheel.radius + wheel.suspensionDistance + .1f) grounded++;
            }
            // Kerbs and banked corners can unload one or two wheels. Do not keep the
            // starting-grid anchor just because all four tyres did not touch together.
            if (grounded < 2 || !_coordinator.TryGetProgress(_participant, out var progress) || progress.HasFinished) return;
            if (!_coordinator.TryGetRecoveryPose(_participant, _body.position, out var point, out var rotation, out var distance) || distance > 7.5f) return;
            if (!TryCaptureRoadPose(point, rotation)) return;
            _anchorRoutePoint = point;
            _anchorRotation = rotation;
            _anchorPassedCheckpoints = progress.TotalPassedCheckpoints;
            _hasRouteAnchor = true;
            _capturePending = false;
        }

        public bool TryPrepareRecoveryPose()
        {
            if (_coordinator == null || !_coordinator.HasRecoveryRoute) return true;
            if (_resetter == null || _participant == null || _coordinator.State != RaceState.Racing ||
                !_coordinator.TryGetProgress(_participant, out var progress) || progress.HasFinished) return false;
            if (_hasRouteAnchor && _anchorPassedCheckpoints == progress.TotalPassedCheckpoints)
                return TryCaptureRoadPose(_anchorRoutePoint, _anchorRotation);
            if (progress.TotalPassedCheckpoints == 0) return true; // The validated starting-grid pose.

            // An accepted airborne checkpoint still advances the fallback. Resolve its
            // road surface now instead of ever teleporting back to an obsolete lap/sector.
            var checkpoints = _coordinator.OrderedCheckpoints;
            var previous = (progress.NextExpectedCheckpointIndex + checkpoints.Count - 1) % checkpoints.Count;
            var checkpoint = checkpoints[previous];
            if (checkpoint == null || !_coordinator.TryGetRecoveryPose(_participant, checkpoint.Position,
                out var point, out var rotation, out _)) return false;
            return TryCaptureRoadPose(point, rotation);
        }

        private bool TryCaptureRoadPose(Vector3 routePoint, Quaternion rotation)
        {
            var right = rotation * Vector3.right;
            foreach (var lane in RecoveryLanes)
            {
                var candidate = routePoint + right * lane;
                var count = gameObject.scene.GetPhysicsScene().Raycast(candidate + Vector3.up * 4, Vector3.down,
                    _roadHits, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == _roadHits.Length) continue;
                var best = -1;
                var nearestHeight = float.PositiveInfinity;
                for (var i = 0; i < count; i++)
                {
                    var hit = _roadHits[i];
                    if (hit.collider == null || hit.collider.attachedRigidbody != null ||
                        hit.collider.transform.IsChildOf(transform) || hit.normal.y < .7f) continue;
                    var height = Mathf.Abs(hit.point.y - routePoint.y);
                    // Do not select a lower deck, a roof or the ground under an overpass.
                    if (height > 1.5f || height >= nearestHeight) continue;
                    nearestHeight = height;
                    best = i;
                }
                if (best >= 0 && _resetter.TryCaptureSafeResetPose(_roadHits[best].point + Vector3.up * .35f, rotation)) return true;
            }
            return false; // Wait for a free lane; never fall back to the starting grid.
        }
    }
}
