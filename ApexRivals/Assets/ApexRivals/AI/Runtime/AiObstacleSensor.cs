using ApexRivals.Race.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// Forward sphere cast for loose physics props in the driver's way. Racers are ignored (traffic
    /// perception knows how to pass them), as are static colliders: walls are handled by steering and
    /// the stuck escalation, and ramps are part of the track.
    /// </summary>
    public sealed class AiObstacleSensor
    {
        private const float StoppingMargin = 3f;
        private const float MinimumProbeDistance = 8f;
        private const float ProbeSecondsAhead = 1.2f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>Highest speed from which the car can still stop before the nearest prop; infinity when clear.</summary>
        public float ProbeSpeedLimit(Transform vehicle, Rigidbody body, float speed, LayerMask layers, float radius, float height, float deceleration)
        {
            var forward = Vector3.ProjectOnPlane(vehicle.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                return float.PositiveInfinity;
            }

            var origin = vehicle.position + Vector3.up * height;
            var distance = Mathf.Max(MinimumProbeDistance, speed * ProbeSecondsAhead);
            var count = Physics.SphereCastNonAlloc(origin, radius, forward.normalized, _hits, distance, layers, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var hit = _hits[index];
                if (hit.collider == null || hit.rigidbody == null || hit.rigidbody == body || hit.collider.transform.IsChildOf(vehicle)
                    || hit.rigidbody.TryGetComponent<RaceParticipant>(out _))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
            }

            return float.IsPositiveInfinity(nearest)
                ? float.PositiveInfinity
                : AiRacecraftRules.CalculateObstacleSpeedLimit(nearest, StoppingMargin, deceleration);
        }
    }
}
