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
            waypointReachDistance = Mathf.Max(AiDriverRules.MinimumReachDistance, waypointReachDistance);
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
