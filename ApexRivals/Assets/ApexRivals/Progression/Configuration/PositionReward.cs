using System;
using UnityEngine;

namespace ApexRivals.Progression.Configuration
{
    [Serializable]
    public struct PositionReward
    {
        [SerializeField, Min(1)]
        private int position;

        [SerializeField, Min(0)]
        private int currencyReward;

        public PositionReward(int position, int currencyReward)
        {
            this.position = position;
            this.currencyReward = currencyReward;
        }

        public int Position => position;
        public int CurrencyReward => currencyReward;
    }
}
