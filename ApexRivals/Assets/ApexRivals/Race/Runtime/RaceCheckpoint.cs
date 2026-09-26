using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ApexRivals.Race.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RaceCheckpoint : MonoBehaviour
    {
        [SerializeField]
        private RaceCoordinator raceCoordinator;

        [SerializeField, Min(0)]
        private int checkpointIndex;

        [SerializeField]
        private Color gizmoColor = new Color(0.1f, 0.75f, 1f, 0.35f);

        // Presentation-only hook (e.g. RaceCheckpointVisual instance). Purely visual:
        // no collider, no gameplay effect. May be null for checkpoints without a
        // visual gate. The trigger volume above remains the sole source of truth for
        // checkpoint detection - this field never participates in OnTriggerEnter.
        [SerializeField]
        private GameObject visualMarker;

        public int CheckpointIndex => checkpointIndex;
        public Vector3 Position => transform.position;

        public void SetVisualActive(bool active)
        {
            if (visualMarker != null && visualMarker.activeSelf != active)
            {
                visualMarker.SetActive(active);
            }
        }

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (raceCoordinator == null)
            {
                return;
            }

            var participant = other.GetComponentInParent<RaceParticipant>();
            if (participant == null)
            {
                return;
            }

            raceCoordinator.TryPassCheckpoint(participant, this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            var size = box != null ? box.size : transform.localScale;
            var center = box != null ? transform.TransformPoint(box.center) : transform.position;

            var isActive = visualMarker != null && visualMarker.activeSelf;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = isActive ? Color.green : Color.white;
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 5f);

            Handles.Label(transform.position + Vector3.up * (size.y * 0.5f + 1f), $"CP {checkpointIndex}{(isActive ? " (active)" : string.Empty)}");
        }
#endif
    }
}
