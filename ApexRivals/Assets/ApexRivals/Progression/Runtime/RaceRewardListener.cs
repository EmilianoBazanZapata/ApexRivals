using ApexRivals.Progression.Configuration;
using ApexRivals.Race.Runtime;
using UnityEngine;

namespace ApexRivals.Progression.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceRewardListener : MonoBehaviour
    {
        [SerializeField]
        private RaceCoordinator raceCoordinator;

        [SerializeField]
        private PlayerProgressionComponent progressionComponent;

        [SerializeField]
        private RaceRewardDefinition rewardDefinition;

        [SerializeField]
        private string playerRacerId = "Player";

        private RaceRewardService _rewardService;

        private void OnEnable()
        {
            if (raceCoordinator == null || progressionComponent == null || rewardDefinition == null)
            {
                return;
            }

            _rewardService = new RaceRewardService(progressionComponent.State, rewardDefinition.CreateRewardTable());
            raceCoordinator.RacerFinished += OnRacerFinished;
        }

        private void OnDisable()
        {
            if (raceCoordinator != null)
            {
                raceCoordinator.RacerFinished -= OnRacerFinished;
            }
        }

        private void OnRacerFinished(RacerFinishedEvent raceEvent)
        {
            if (raceEvent.Result.RacerId != playerRacerId)
            {
                return;
            }

            _rewardService?.TryAwardReward(raceEvent.Result, out _);
        }
    }
}
