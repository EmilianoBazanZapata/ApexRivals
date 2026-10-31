using ApexRivals.Vehicle.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NitroHudUguiView : MonoBehaviour
    {
        [Header("Display text")]
        [SerializeField] private string activeLabel = "NITRO ACTIVE";
        [SerializeField] private string chargingLabel = "DRIFT + NITRO";
        [SerializeField] private string fullLabel = "NITRO FULL";
        [SerializeField] private string idleLabel = "NITRO";

        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private Image chargeFill;
        [SerializeField] private Text statusText;
        [SerializeField] private Text chargeText;
        private PlayerVehicleNitro _source;
        private int _lastPercent = -1;
        private int _lastState = -1;

        public void Bind(PlayerVehicleNitro source)
        {
            _source = source;
            _lastPercent = _lastState = -1;
            gameObject.SetActive(source != null);
            if (source != null) Render();
        }

        private void Update() => Render();

        private void Render()
        {
            if (_source == null)
            {
                gameObject.SetActive(false);
                return;
            }
            var charge = Mathf.Clamp01(_source.NormalizedCharge);
            var percent = Mathf.FloorToInt(charge * 100f);
            if (chargeFill != null)
                chargeFill.rectTransform.anchorMax = new Vector2(charge, 1f);
            if (_lastPercent != percent)
            {
                UguiViewText.Set(chargeText, percent + "%");
                _lastPercent = percent;
            }
            var state = _source.IsBoosting ? 2 : _source.IsCharging ? 1 : percent >= 100 ? 3 : 0;
            if (_lastState == state) return;
            UguiViewText.Set(statusText, state == 2 ? activeLabel : state == 1 ? chargingLabel : state == 3 ? fullLabel : idleLabel);
            if (chargeFill != null) chargeFill.color = state == 2 ? Palette.FocusedText : Palette.PrimaryRed;
            _lastState = state;
        }
    }
}
