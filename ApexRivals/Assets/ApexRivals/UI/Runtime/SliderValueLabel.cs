using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Shows a 0-1 slider as a percentage next to it ("80%").</summary>
    [DisallowMultipleComponent]
    public sealed class SliderValueLabel : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Text label;

        private float _displayedValue = float.NaN;

        private void LateUpdate()
        {
            if (slider != null && _displayedValue != slider.normalizedValue) Show(slider.value);
        }

        private void OnEnable()
        {
            if (slider != null)
            {
                slider.onValueChanged.AddListener(Show);
                Show(slider.value);
            }
        }

        private void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(Show);
            }
        }

        private void Show(float value)
        {
            _displayedValue = slider != null ? slider.normalizedValue : value;
            UguiViewText.Set(label, Format(_displayedValue));
        }

        public static string Format(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
