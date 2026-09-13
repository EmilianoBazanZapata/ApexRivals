using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Input.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DrivingInputGate : MonoBehaviour, IDrivingInputProvider
    {
        [SerializeField]
        private MonoBehaviour sourceInputProvider;

        [SerializeField]
        private bool drivingAllowed;

        private IDrivingInputProvider _source;

        public bool DrivingAllowed => drivingAllowed;

        public DrivingInput CurrentInput
        {
            get
            {
                if (!drivingAllowed || _source == null)
                {
                    return DrivingInput.Neutral;
                }

                return _source.CurrentInput;
            }
        }

        private void Awake()
        {
            _source = sourceInputProvider as IDrivingInputProvider;
        }

        public void SetDrivingAllowed(bool isAllowed)
        {
            drivingAllowed = isAllowed;
            Debug.Log($"[VEHICLE_DIAG] {name} DrivingInputGate.SetDrivingAllowed({isAllowed}) source={(_source != null ? _source.GetType().Name : "null")}", this);

            if (!drivingAllowed)
            {
                _source?.ConsumeResetRequest();
                _source?.ConsumeRecoveryRequest();
            }
        }

        public void Configure(IDrivingInputProvider inputProvider, bool isDrivingAllowed)
        {
            sourceInputProvider = inputProvider as MonoBehaviour;
            _source = inputProvider;
            SetDrivingAllowed(isDrivingAllowed);
        }

        public void ConsumeResetRequest()
        {
            _source?.ConsumeResetRequest();
        }

        public void ConsumeRecoveryRequest()
        {
            _source?.ConsumeRecoveryRequest();
        }
    }
}
