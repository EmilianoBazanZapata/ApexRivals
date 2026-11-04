using System;
using UnityEngine;

namespace ApexRivals.Vehicle.Configuration
{
    [Serializable]
    public struct WheelFrictionSettings
    {
        [Tooltip("Dimensionless WheelCollider slip value where this tire direction reaches peak grip. Higher values reach peak grip later; lower values reach it sooner.")]
        [Min(0f)] public float extremumSlip;
        [Tooltip("Dimensionless peak grip multiplier for this tire direction. Higher values increase available grip; lower values reduce it.")]
        [Min(0f)] public float extremumValue;
        [Tooltip("Dimensionless WheelCollider slip value where this tire direction reaches post-peak grip. Higher values make the transition more gradual; lower values reach the sliding region sooner.")]
        [Min(0f)] public float asymptoteSlip;
        [Tooltip("Dimensionless grip multiplier retained after peak slip. Higher values retain more grip while sliding; lower values make sustained slides looser.")]
        [Min(0f)] public float asymptoteValue;
        [Tooltip("Dimensionless overall multiplier for this tire direction's friction curve. Higher values increase grip across the curve; lower values reduce it.")]
        [Min(0f)] public float stiffness;

        public WheelFrictionCurve CreateCurve()
        {
            return new WheelFrictionCurve
            {
                extremumSlip = Mathf.Max(0f, extremumSlip),
                extremumValue = Mathf.Max(0f, extremumValue),
                asymptoteSlip = Mathf.Max(0f, asymptoteSlip),
                asymptoteValue = Mathf.Max(0f, asymptoteValue),
                stiffness = Mathf.Max(0f, stiffness)
            };
        }
    }
}
