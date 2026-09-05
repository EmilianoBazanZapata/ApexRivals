namespace ApexRivals.Vehicle.Runtime
{
    public readonly struct ArcadeVehicleStats
    {
        public ArcadeVehicleStats(
            float forwardAcceleration,
            float reverseAcceleration,
            float brakeForce,
            float maximumForwardSpeed,
            float maximumReverseSpeed,
            float steeringStrength,
            float minimumSteeringResponse,
            float driftSteeringMultiplier,
            float normalLateralGrip,
            float driftLateralGrip,
            float downforce,
            float groundCheckDistance,
            int groundLayerMask,
            float resetHeightOffset,
            float reverseEntrySpeed)
        {
            ForwardAcceleration = forwardAcceleration;
            ReverseAcceleration = reverseAcceleration;
            BrakeForce = brakeForce;
            MaximumForwardSpeed = maximumForwardSpeed;
            MaximumReverseSpeed = maximumReverseSpeed;
            SteeringStrength = steeringStrength;
            MinimumSteeringResponse = minimumSteeringResponse;
            DriftSteeringMultiplier = driftSteeringMultiplier;
            NormalLateralGrip = normalLateralGrip;
            DriftLateralGrip = driftLateralGrip;
            Downforce = downforce;
            GroundCheckDistance = groundCheckDistance;
            GroundLayerMask = groundLayerMask;
            ResetHeightOffset = resetHeightOffset;
            ReverseEntrySpeed = reverseEntrySpeed;
        }

        public float ForwardAcceleration { get; }
        public float ReverseAcceleration { get; }
        public float BrakeForce { get; }
        public float MaximumForwardSpeed { get; }
        public float MaximumReverseSpeed { get; }
        public float SteeringStrength { get; }
        public float MinimumSteeringResponse { get; }
        public float DriftSteeringMultiplier { get; }
        public float NormalLateralGrip { get; }
        public float DriftLateralGrip { get; }
        public float Downforce { get; }
        public float GroundCheckDistance { get; }
        public int GroundLayerMask { get; }
        public float ResetHeightOffset { get; }
        public float ReverseEntrySpeed { get; }
    }
}
