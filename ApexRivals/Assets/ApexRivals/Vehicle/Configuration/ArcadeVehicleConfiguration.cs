using UnityEngine;

namespace ApexRivals.Vehicle.Configuration
{
    [CreateAssetMenu(fileName = "ArcadeVehicleConfiguration", menuName = "Apex Rivals/Vehicle/Arcade Vehicle Configuration")]
    public sealed class ArcadeVehicleConfiguration : ScriptableObject
    {
        [Header("Acceleration")]
        [Min(0f)]
        public float forwardAcceleration = 45f;

        [Min(0f)]
        public float reverseAcceleration = 25f;

        [Min(0f)]
        public float brakeForce = 70f;

        [Min(0f)]
        public float maximumForwardSpeed = 34f;

        [Min(0f)]
        public float maximumReverseSpeed = 12f;

        [Header("Steering")]
        [Min(0f)]
        public float steeringStrength = 95f;

        [Range(0f, 1f)]
        public float minimumSteeringResponse = 0.18f;

        [Min(0f)]
        public float driftSteeringMultiplier = 1.25f;

        [Header("Grip")]
        [Range(0f, 1f)]
        public float normalLateralGrip = 0.82f;

        [Range(0f, 1f)]
        public float driftLateralGrip = 0.42f;

        [Header("Grounding")]
        [Min(0f)]
        public float downforce = 35f;

        [Min(0.01f)]
        public float groundCheckDistance = 0.75f;

        public LayerMask groundLayerMask = ~0;

        [Header("Reset")]
        [Min(0f)]
        public float resetHeightOffset = 0.75f;

        [Min(0f)]
        public float reverseEntrySpeed = 0.5f;
    }
}
