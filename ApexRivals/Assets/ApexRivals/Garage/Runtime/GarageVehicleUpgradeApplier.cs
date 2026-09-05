using ApexRivals.Progression.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Garage.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageVehicleUpgradeApplier : MonoBehaviour
    {
        [SerializeField]
        private ArcadeVehicleController vehicleController;

        private GarageService _garageService;
        private PlayerProgressionState _progressionState;

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
            if (vehicleController == null)
            {
                vehicleController = GetComponent<ArcadeVehicleController>();
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
            if (vehicleController == null || _garageService == null)
            {
                return;
            }

            vehicleController.ApplyEffectiveStats(_garageService.CalculateEffectiveStats());
        }

        private void Unsubscribe()
        {
            if (_progressionState != null)
            {
                _progressionState.Changed -= OnProgressionChanged;
            }
        }
    }
}
