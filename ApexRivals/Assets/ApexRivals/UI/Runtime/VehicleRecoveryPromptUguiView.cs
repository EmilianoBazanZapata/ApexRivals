using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VehicleRecoveryPromptUguiView : MonoBehaviour, IVehicleRecoveryPromptView
    {
        [SerializeField] private Text promptText;

        private bool _isVisible;

        private void Awake()
        {
            // Keep the message authored on promptText; only visibility changes at runtime.
            _isVisible = true;
            SetVisible(false);
        }

        public void Render(bool isVisible)
        {
            SetVisible(isVisible);
        }

        private void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible)
            {
                return;
            }

            _isVisible = isVisible;
            gameObject.SetActive(isVisible);
        }
    }
}
