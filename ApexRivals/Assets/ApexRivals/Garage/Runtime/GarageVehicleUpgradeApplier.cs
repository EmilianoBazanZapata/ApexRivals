using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Garage.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageVehicleUpgradeApplier : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour vehicleController;

        private GarageService _garageService;
        private PlayerProgressionState _progressionState;
        private IVehicleRuntime _vehicleRuntime;

        public void Initialize(GarageService garageService, PlayerProgressionState progressionState)
        {
            Unsubscribe();
            _garageService = garageService;
            _progressionState = progressionState;

            if (_progressionState != null)
            {
                _progressionState.Changed += OnProgressionChanged;
            }

            ApplyCurrentStats();
        }

        private void Awake()
        {
            if (!TryValidateVehicleRuntime(out var message))
            {
                Debug.LogError(message, this);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnProgressionChanged(ProgressionStateChangedEvent progressionEvent)
        {
            ApplyCurrentStats();
        }

        private void ApplyCurrentStats()
        {
            if (_vehicleRuntime == null || _garageService == null)
            {
                return;
            }

            _vehicleRuntime.ApplyPerformanceStats(_garageService.CalculateEffectiveStats());
        }

        private void Unsubscribe()
        {
            if (_progressionState != null)
            {
                _progressionState.Changed -= OnProgressionChanged;
            }
        }

        public bool TryValidateVehicleRuntime(out string message)
        {
            if (!VehicleRuntimeResolver.TryResolveExactlyOne(this, vehicleController, out var runtime, out message))
            {
                _vehicleRuntime = null;
                return false;
            }

            _vehicleRuntime = runtime;
            vehicleController = runtime as MonoBehaviour;
            return true;
        }
    }
}
