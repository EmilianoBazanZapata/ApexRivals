using System;
using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceParticipant : MonoBehaviour
    {
        [SerializeField]
        private RaceCoordinator raceCoordinator;

        [SerializeField]
        private string racerId = "Player";

        [SerializeField]
        private Transform progressTransform;

        public string RacerId => racerId;
        public Transform ProgressTransform => progressTransform != null ? progressTransform : transform;

        public void Configure(RaceCoordinator coordinator, string participantId, Transform participantProgressTransform)
        {
            if (isActiveAndEnabled && raceCoordinator != null)
            {
                raceCoordinator.UnregisterRacer(this);
            }

            raceCoordinator = coordinator;
            racerId = string.IsNullOrWhiteSpace(participantId) ? racerId : participantId;
            progressTransform = participantProgressTransform;

            if (isActiveAndEnabled && raceCoordinator != null)
            {
                raceCoordinator.RegisterRacer(this);
            }
        }

        private void OnEnable()
        {
            if (raceCoordinator != null)
            {
                raceCoordinator.RegisterRacer(this);
            }
        }

        private void OnDisable()
        {
            if (raceCoordinator != null)
            {
                raceCoordinator.UnregisterRacer(this);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(racerId))
            {
                racerId = Guid.NewGuid().ToString("N");
            }
        }
    }
}
