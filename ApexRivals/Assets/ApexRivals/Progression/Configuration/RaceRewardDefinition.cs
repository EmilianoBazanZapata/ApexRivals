using System;
using System.Collections.Generic;
using ApexRivals.Progression.Runtime;
using UnityEngine;

namespace ApexRivals.Progression.Configuration
{
    [CreateAssetMenu(fileName = "RaceRewardDefinition", menuName = "Apex Rivals/Progression/Race Reward Definition")]
    public sealed class RaceRewardDefinition : ScriptableObject
    {
        [SerializeField]
        private PositionReward[] rewards =
        {
            new PositionReward(1, 500),
            new PositionReward(2, 300),
            new PositionReward(3, 200)
        };

        [SerializeField, Min(0)]
        private int fallbackReward = 100;

        public RaceRewardTable CreateRewardTable()
        {
            var rewardsByPosition = new Dictionary<int, int>();
            for (var index = 0; index < rewards.Length; index++)
            {
                if (rewards[index].Position > 0 && rewards[index].CurrencyReward >= 0)
                {
                    rewardsByPosition[rewards[index].Position] = rewards[index].CurrencyReward;
                }
            }

            return new RaceRewardTable(rewardsByPosition, fallbackReward);
        }

        private void OnValidate()
        {
            fallbackReward = Math.Max(0, fallbackReward);

            for (var index = 0; index < rewards.Length; index++)
            {
                rewards[index] = new PositionReward(
                    Math.Max(1, rewards[index].Position),
                    Math.Max(0, rewards[index].CurrencyReward));
            }
        }
    }
}
