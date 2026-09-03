using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DevelopmentRaceStatePresenter : MonoBehaviour
    {
        [SerializeField]
        private RaceCoordinator raceCoordinator;

        [SerializeField]
        private RaceParticipant observedParticipant;

        [Header("Debug Output")]
        [SerializeField]
        private RaceState state;

        [SerializeField]
        private float countdownRemaining;

        [SerializeField]
        private float raceTime;

        [SerializeField]
        private int currentLap;

        [SerializeField]
        private int currentPosition;

        [SerializeField]
        private string finalResult = string.Empty;

        private void OnEnable()
        {
            if (raceCoordinator == null)
            {
                return;
            }

            raceCoordinator.RacerFinished += OnRacerFinished;
        }

        private void Update()
        {
            if (raceCoordinator == null)
            {
                return;
            }

            state = raceCoordinator.State;
            countdownRemaining = raceCoordinator.CountdownRemaining;
            raceTime = raceCoordinator.ElapsedRaceTime;

            if (observedParticipant != null && raceCoordinator.TryGetProgress(observedParticipant, out var progress))
            {
                currentLap = progress.CurrentLap;
                currentPosition = raceCoordinator.GetLivePosition(progress.RacerId);
            }
        }

        private void OnDisable()
        {
            if (raceCoordinator == null)
            {
                return;
            }

            raceCoordinator.RacerFinished -= OnRacerFinished;
        }

        private void OnRacerFinished(RacerFinishedEvent raceEvent)
        {
            finalResult = $"{raceEvent.Result.RacerId}: P{raceEvent.Result.FinalPosition} in {raceEvent.Result.FinishTime:0.000}s";
        }
    }
}
