using System;
using ApexRivals.Garage.Runtime;
using UnityEngine;

namespace ApexRivals.Garage.Configuration
{
    [Serializable]
    public struct UpgradeLevelDefinition
    {
        [SerializeField, Min(0)]
        private int price;

        [SerializeField, Min(0f)]
        private float accelerationMultiplier;

        [SerializeField, Min(0f)]
        private float maximumSpeedMultiplier;

        [SerializeField, Min(0f)]
        private float steeringMultiplier;

        [SerializeField, Range(0f, 1f)]
        private float normalGripBonus;

        [SerializeField, Range(0f, 1f)]
        private float driftGripBonus;

        public UpgradeLevelDefinition(
            int price,
            float accelerationMultiplier,
            float maximumSpeedMultiplier,
            float steeringMultiplier,
            float normalGripBonus,
            float driftGripBonus)
        {
            this.price = price;
            this.accelerationMultiplier = accelerationMultiplier;
            this.maximumSpeedMultiplier = maximumSpeedMultiplier;
            this.steeringMultiplier = steeringMultiplier;
            this.normalGripBonus = normalGripBonus;
            this.driftGripBonus = driftGripBonus;
        }

        public UpgradeLevel ToRuntimeLevel()
        {
            return new UpgradeLevel(
                Mathf.Max(0, price),
                new UpgradeStatModifier(
                    Mathf.Max(0f, accelerationMultiplier),
                    Mathf.Max(0f, maximumSpeedMultiplier),
                    Mathf.Max(0f, steeringMultiplier),
                    Mathf.Clamp01(normalGripBonus),
                    Mathf.Clamp01(driftGripBonus)));
        }
    }
}
