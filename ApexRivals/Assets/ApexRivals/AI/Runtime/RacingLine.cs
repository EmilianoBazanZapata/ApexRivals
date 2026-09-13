using System;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RacingLine : MonoBehaviour
    {
        [SerializeField]
        private Transform[] waypoints = Array.Empty<Transform>();

        [SerializeField, Min(AiDriverRules.MinimumReachDistance)]
        private float waypointReachDistance = 5f;

        [SerializeField]
        private Color gizmoColor = new Color(0f, 0.85f, 1f, 1f);

        public int WaypointCount => waypoints != null ? waypoints.Length : 0;
        public float WaypointReachDistance => waypointReachDistance;
        public bool IsValid => ValidateWaypoints();

        public void Configure(Transform[] orderedWaypoints)
        {
            waypoints = orderedWaypoints ?? Array.Empty<Transform>();
        }

        public int WrapIndex(int index)
        {
            return AiDriverRules.WrapWaypointIndex(index, WaypointCount);
        }

        public Vector3 GetWaypointPosition(int index)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return transform.position;
            }

            var waypoint = waypoints[WrapIndex(index)];
            return waypoint != null ? waypoint.position : transform.position;
        }

        public Vector3 GetWaypointForward(int index)
        {
            if (WaypointCount < AiDriverRules.MinimumWaypointCount)
            {
                return transform.forward;
            }

            var current = GetWaypointPosition(index);
            var next = GetWaypointPosition(WrapIndex(index + 1));
            var forward = Vector3.ProjectOnPlane(next - current, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        public int GetNearestWaypointIndex(Vector3 position)
        {
            if (WaypointCount == 0)
            {
                return 0;
            }

            var nearestIndex = 0;
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < WaypointCount; index++)
            {
                var distance = (GetWaypointPosition(index) - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            }

            return nearestIndex;
        }

        public bool TryGetArcLookaheadTarget(
            Vector3 position,
            int startSegmentIndex,
            float desiredArcDistance,
            int maximumSegments,
            out int targetSegmentIndex,
            out Vector3 projectedStartPoint,
            out Vector3 targetPoint,
            out float actualArcDistance)
        {
            targetSegmentIndex = 0;
            projectedStartPoint = transform.position;
            targetPoint = transform.position;
            actualArcDistance = 0f;

            if (!IsValid)
            {
                return false;
            }

            var segmentIndex = WrapIndex(startSegmentIndex);
            var segmentStart = GetWaypointPosition(segmentIndex);
            var segmentEnd = GetWaypointPosition(segmentIndex + 1);
            var initialSegment = segmentEnd - segmentStart;
            var initialLength = initialSegment.magnitude;
            if (initialLength < 0.0001f)
            {
                return false;
            }

            var interpolation = Mathf.Clamp01(Vector3.Dot(position - segmentStart, initialSegment) / initialSegment.sqrMagnitude);
            projectedStartPoint = segmentStart + initialSegment * interpolation;
            var remainingDistance = Mathf.Max(0f, desiredArcDistance);
            var segmentOffset = 0;
            var currentStart = projectedStartPoint;
            var currentEnd = segmentEnd;

            while (segmentOffset < Mathf.Max(1, maximumSegments))
            {
                var segment = currentEnd - currentStart;
                var segmentLength = segment.magnitude;
                if (segmentLength > 0.0001f)
                {
                    if (remainingDistance <= segmentLength)
                    {
                        targetSegmentIndex = segmentIndex;
                        targetPoint = currentStart + segment * (remainingDistance / segmentLength);
                        actualArcDistance += remainingDistance;
                        return true;
                    }

                    remainingDistance -= segmentLength;
                    actualArcDistance += segmentLength;
                }

                segmentOffset++;
                segmentIndex = WrapIndex(segmentIndex + 1);
                currentStart = GetWaypointPosition(segmentIndex);
                currentEnd = GetWaypointPosition(segmentIndex + 1);
            }

            targetSegmentIndex = segmentIndex;
            targetPoint = currentStart;
            return actualArcDistance > 0f;
        }

        public float EstimateUpcomingCurvature(int startSegmentIndex, int segmentCount)
        {
            if (!IsValid || segmentCount <= 0)
            {
                return 0f;
            }

            var totalCurvature = 0f;
            var validTurns = 0;
            for (var offset = 0; offset < segmentCount; offset++)
            {
                var currentForward = GetWaypointForward(startSegmentIndex + offset);
                var nextForward = GetWaypointForward(startSegmentIndex + offset + 1);
                if (currentForward.sqrMagnitude < 0.0001f || nextForward.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                totalCurvature += Vector3.Angle(currentForward, nextForward) / 180f;
                validTurns++;
            }

            return validTurns > 0 ? Mathf.Clamp01(totalCurvature / validTurns) : 0f;
        }

        public bool TryGetNearestPoint(
            Vector3 position,
            out int nearestSegmentIndex,
            out Vector3 nearestPoint,
            out Vector3 trackForward,
            out float distance)
        {
            nearestSegmentIndex = 0;
            nearestPoint = transform.position;
            trackForward = transform.forward;
            distance = float.PositiveInfinity;

            if (!IsValid)
            {
                return false;
            }

            var bestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < waypoints.Length; index++)
            {
                var start = waypoints[index].position;
                var end = waypoints[WrapIndex(index + 1)].position;
                var segment = end - start;
                var segmentLengthSquared = segment.sqrMagnitude;
                if (segmentLengthSquared < 0.0001f)
                {
                    continue;
                }

                var t = Mathf.Clamp01(Vector3.Dot(position - start, segment) / segmentLengthSquared);
                var point = start + segment * t;
                var candidateDistanceSquared = (position - point).sqrMagnitude;
                if (candidateDistanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = candidateDistanceSquared;
                nearestSegmentIndex = index;
                nearestPoint = point;
                trackForward = Vector3.ProjectOnPlane(segment, Vector3.up).normalized;
            }

            if (trackForward.sqrMagnitude < 0.0001f)
            {
                trackForward = Vector3.forward;
            }

            distance = Mathf.Sqrt(bestDistanceSquared);
            return !float.IsInfinity(distance);
        }

        public bool TryGetNearestPointInProgressRange(
            Vector3 position,
            int segmentStartIndex,
            int segmentEndIndex,
            int forwardMargin,
            out int nearestSegmentIndex,
            out Vector3 nearestPoint,
            out Vector3 trackForward,
            out float distance)
        {
            nearestSegmentIndex = 0;
            nearestPoint = transform.position;
            trackForward = transform.forward;
            distance = float.PositiveInfinity;

            if (!IsValid)
            {
                return false;
            }

            var maximumSegmentDistance = AiRecoveryRules.GetForwardIndexDistance(segmentStartIndex, segmentEndIndex, WaypointCount)
                + Mathf.Max(0, forwardMargin);
            for (var offset = 0; offset <= maximumSegmentDistance; offset++)
            {
                var index = WrapIndex(segmentStartIndex + offset);
                var from = GetWaypointPosition(index);
                var to = GetWaypointPosition(WrapIndex(index + 1));
                var segment = to - from;
                var segmentLengthSquared = segment.sqrMagnitude;
                if (segmentLengthSquared < 0.0001f)
                {
                    continue;
                }

                var interpolation = Mathf.Clamp01(Vector3.Dot(position - from, segment) / segmentLengthSquared);
                var candidatePoint = from + segment * interpolation;
                var candidateDistance = Vector3.Distance(position, candidatePoint);
                if (candidateDistance >= distance)
                {
                    continue;
                }

                nearestSegmentIndex = index;
                nearestPoint = candidatePoint;
                trackForward = Vector3.ProjectOnPlane(segment, Vector3.up).normalized;
                distance = candidateDistance;
            }

            return !float.IsInfinity(distance);
        }

        private bool ValidateWaypoints()
        {
            if (waypoints == null || waypoints.Length < AiDriverRules.MinimumWaypointCount)
            {
                return false;
            }

            for (var index = 0; index < waypoints.Length; index++)
            {
                if (waypoints[index] == null)
                {
                    return false;
                }

                for (var compareIndex = index + 1; compareIndex < waypoints.Length; compareIndex++)
                {
                    if (waypoints[index] == waypoints[compareIndex])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void OnValidate()
        {
            waypointReachDistance = AiDriverRules.ClampReachDistance(waypointReachDistance);
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            Gizmos.color = gizmoColor;

            for (var index = 0; index < waypoints.Length; index++)
            {
                var waypoint = waypoints[index];
                if (waypoint == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(waypoint.position, waypointReachDistance * 0.15f);

                var nextWaypoint = waypoints[WrapIndex(index + 1)];
                if (nextWaypoint != null)
                {
                    Gizmos.DrawLine(waypoint.position, nextWaypoint.position);
                }
            }
        }
    }
}
