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

            if (!drivingAllowed)
            {
                _source?.ConsumeResetRequest();
            }
        }

        public void ConsumeResetRequest()
        {
            _source?.ConsumeResetRequest();
        }
    }
}
