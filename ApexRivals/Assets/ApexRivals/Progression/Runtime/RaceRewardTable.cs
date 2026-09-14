using System;
using System.Collections.Generic;

namespace ApexRivals.Progression.Runtime
{
    public sealed class RaceRewardTable
    {
        private readonly Dictionary<int, int> _rewardsByPosition;

        public RaceRewardTable(IReadOnlyDictionary<int, int> rewardsByPosition, int fallbackReward)
        {
            if (fallbackReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fallbackReward), "Fallback reward cannot be negative.");
            }

            _rewardsByPosition = new Dictionary<int, int>();
            if (rewardsByPosition != null)
            {
                foreach (var reward in rewardsByPosition)
                {
                    if (reward.Key <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(rewardsByPosition), "Reward positions must be positive.");
                    }

                    if (reward.Value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(rewardsByPosition), "Reward values cannot be negative.");
                    }

                    _rewardsByPosition[reward.Key] = reward.Value;
                }
            }

            FallbackReward = fallbackReward;
        }

        public int FallbackReward { get; }

        public bool TryGetReward(int finalPosition, out int reward)
        {
            if (finalPosition <= 0)
            {
                reward = 0;
                return false;
            }

            reward = _rewardsByPosition.TryGetValue(finalPosition, out var configuredReward)
                ? configuredReward
                : FallbackReward;
            return true;
        }
    }
}
