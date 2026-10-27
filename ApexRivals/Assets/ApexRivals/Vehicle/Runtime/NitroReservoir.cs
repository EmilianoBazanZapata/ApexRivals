using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>Race-local nitro earned from actual sliding, independently of handbrake input.</summary>
    public sealed class NitroReservoir
    {
        public const float MinimumDriftSpeed = 8f;
        public const float MinimumDriftAngle = 12f;
        public const float MaximumDriftAngle = 65f;
        public const float QualificationDuration = 0.35f;
        private readonly float _capacity;
        private readonly float _chargeRate;
        private readonly float _consumptionRate;
        private float _driftDuration;

        public NitroReservoir(float capacity = 100f, float chargeRate = 8f, float consumptionRate = 50f)
        {
            _capacity = Mathf.Max(1f, capacity);
            _chargeRate = Mathf.Max(0f, chargeRate);
            _consumptionRate = Mathf.Max(0.01f, consumptionRate);
        }

        public float Charge { get; private set; }
        public float NormalizedCharge => Charge / _capacity;
        public bool IsCharging { get; private set; }
        public bool IsBoosting { get; private set; }

        /// <summary>Returns the fraction of this step for which fuel funded a boost.</summary>
        public float Step(float deltaTime, bool drivingAllowed, bool supported,
            float forwardSpeed, float slipAngle, float throttle, float brake, bool nitroHeld)
        {
            IsCharging = IsBoosting = false;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return 0f;
            var angle = Mathf.Abs(slipAngle);
            if (!drivingAllowed || !supported || forwardSpeed < 0f || angle > MaximumDriftAngle)
            {
                Interrupt();
                return 0f;
            }

            if (nitroHeld)
            {
                _driftDuration = 0f;
                if (throttle <= 0.1f || brake > 0.1f || Charge <= 0f) return 0f;
                var requested = _consumptionRate * deltaTime;
                var consumed = Mathf.Min(Charge, requested);
                Charge = Mathf.Max(0f, Charge - consumed);
                IsBoosting = consumed > 0f;
                return consumed / requested;
            }

            if (forwardSpeed < MinimumDriftSpeed || angle < MinimumDriftAngle)
            {
                _driftDuration = 0f;
                return 0f;
            }

            var before = Mathf.Max(0f, _driftDuration - QualificationDuration);
            _driftDuration += deltaTime;
            var qualifiedTime = Mathf.Max(0f, _driftDuration - QualificationDuration) - before;
            var quality = Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(MinimumDriftAngle, 40f, angle));
            var previous = Charge;
            Charge = Mathf.Min(_capacity, Charge + qualifiedTime * _chargeRate * quality);
            IsCharging = Charge > previous;
            return 0f;
        }

        public void Interrupt()
        {
            _driftDuration = 0f;
            IsCharging = IsBoosting = false;
        }
    }
}
