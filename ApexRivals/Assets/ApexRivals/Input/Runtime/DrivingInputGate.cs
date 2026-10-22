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
            // Preserve local roof-recovery wiring when its serialized reference is absent.
            // Input supplies the contract; Vehicle does not resolve a concrete Input type.
            var recovery = GetComponent<VehicleRoofRecoveryDetector>();
            if (recovery != null) recovery.ConfigureRecoveryInputIfMissing(this);
        }

        public void SetDrivingAllowed(bool isAllowed)
        {
            drivingAllowed = isAllowed;

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
