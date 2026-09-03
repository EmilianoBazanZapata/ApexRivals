using UnityEngine;

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

        public int CheckpointIndex => checkpointIndex;
        public Vector3 Position => transform.position;

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

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(transform.position, transform.localScale);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
}
