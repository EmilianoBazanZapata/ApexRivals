using ApexRivals.Race.Runtime;

namespace ApexRivals.Progression.Runtime
{
    public sealed class RaceRewardService
    {
        private readonly PlayerProgressionState _progressionState;
        private readonly RaceRewardTable _rewardTable;

        public RaceRewardService(PlayerProgressionState progressionState, RaceRewardTable rewardTable)
        {
            _progressionState = progressionState;
            _rewardTable = rewardTable;
        }

        public bool TryAwardReward(RaceResult raceResult, out int reward)
        {
            if (_progressionState == null || _rewardTable == null || !_rewardTable.TryGetReward(raceResult.FinalPosition, out reward))
            {
                reward = 0;
                return false;
            }

            if (reward == 0)
            {
                return true;
            }

            return _progressionState.AddCurrency(reward).Succeeded;
        }
    }
}
