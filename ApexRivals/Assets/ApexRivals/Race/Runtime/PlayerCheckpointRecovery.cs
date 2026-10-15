using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    /// <summary>Records a grounded player pose near each accepted checkpoint without changing race progress.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCheckpointRecovery : MonoBehaviour
    {
        private RaceCoordinator _coordinator;
        private RaceParticipant _participant;
        private VehicleResetter _resetter;
        private Rigidbody _body;
        private WheelCollider[] _wheels;
        private bool _capturePending;
        private Vector3 _checkpointPosition;
        private bool _subscribed;

        public void Configure(RaceCoordinator coordinator, RaceParticipant participant, VehicleResetter resetter)
        {
            Unsubscribe();
            _coordinator = coordinator;
            _participant = participant;
            _resetter = resetter;
            _body = GetComponent<Rigidbody>();
            _wheels = GetComponentsInChildren<WheelCollider>();
            _capturePending = false;
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
            if (_capturePending) TryCapture();
        }

        private void TryCapture()
        {
            if (_coordinator == null || _coordinator.State != RaceState.Racing || _resetter == null || _body == null) return;
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
    }
}
