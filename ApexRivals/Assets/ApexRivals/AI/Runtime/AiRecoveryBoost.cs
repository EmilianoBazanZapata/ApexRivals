using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>Temporary post-reset power; restores the exact pre-boost vehicle stats and never stacks.</summary>
    public sealed class AiRecoveryBoost
    {
        public const float Duration = 3f;
        public const float PowerMultiplier = 1.5f;

        private IVehicleRuntime _vehicle;
        private VehiclePerformanceStats _baseline;
        private float _remaining;
        private IVehicleRuntime _armedVehicle;
        private float _armingRemaining;

        public bool IsActive => _remaining > 0f;
        public float Remaining => _remaining;
        public bool IsPending => _armedVehicle != null && _armingRemaining > 0f;

        /// <summary>Offer a boost after reset, without applying power before the car has settled.</summary>
        public void Arm(IVehicleRuntime vehicle)
        {
            Cancel();
            if (vehicle == null || (vehicle is Object unityObject && unityObject == null))
            {
                return;
            }

            _armedVehicle = vehicle;
            _armingRemaining = Duration;
        }

        public void Begin(IVehicleRuntime vehicle)
        {
            Cancel();
            if (vehicle == null || (vehicle is Object unityObject && unityObject == null))
            {
                return;
            }

            _vehicle = vehicle;
            _baseline = vehicle.CurrentPerformanceStats;
            _remaining = Duration;
            vehicle.ApplyPerformanceStats(_baseline.WithPowerScale(PowerMultiplier));
        }

        public void Tick(float deltaTime, bool safeToBoost = true)
        {
            if (IsPending)
            {
                if (safeToBoost)
                {
                    var vehicle = _armedVehicle;
                    Begin(vehicle);
                }
                else
                {
                    _armingRemaining = Mathf.Max(0f, _armingRemaining - Mathf.Max(0f, deltaTime));
                    if (_armingRemaining <= 0f)
                    {
                        Cancel();
                    }
                }

                return;
            }

            if (!IsActive)
            {
                return;
            }

            if (!safeToBoost)
            {
                Cancel();
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - Mathf.Max(0f, deltaTime));
            if (_remaining <= 0f)
            {
                Cancel();
            }
        }

        public void Cancel()
        {
            if (_vehicle != null && !(_vehicle is Object unityObject && unityObject == null))
            {
                _vehicle.ApplyPerformanceStats(_baseline);
            }

            _vehicle = null;
            _remaining = 0f;
            _armedVehicle = null;
            _armingRemaining = 0f;
        }
    }
}
