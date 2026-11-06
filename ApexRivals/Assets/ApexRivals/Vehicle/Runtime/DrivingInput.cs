using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    public readonly struct DrivingInput
    {
        public DrivingInput(
            float throttle,
            float brake,
            float steering,
            bool drift,
            bool resetRequested,
            RecoveryInputSource recoveryInputSource = RecoveryInputSource.None)
        {
            Throttle = Mathf.Clamp01(throttle);
            Brake = Mathf.Clamp01(brake);
            Steering = Mathf.Clamp(steering, -1f, 1f);
            Drift = drift;
            ResetRequested = resetRequested;
            RecoveryRequestSource = recoveryInputSource;
        }

        public float Throttle { get; }
        public float Brake { get; }
        public float Steering { get; }
        public bool Drift { get; }
        public bool ResetRequested { get; }
        public RecoveryInputSource RecoveryRequestSource { get; }
        public bool RecoverVehicleRequested => RecoveryRequestSource != RecoveryInputSource.None;

        public static DrivingInput Neutral => new DrivingInput(0f, 0f, 0f, false, false);

        public DrivingInput WithoutResetRequest()
        {
            return new DrivingInput(Throttle, Brake, Steering, Drift, false, RecoveryRequestSource);
        }

        public DrivingInput WithoutRecoveryRequest()
        {
            return new DrivingInput(Throttle, Brake, Steering, Drift, ResetRequested);
        }
    }
}
