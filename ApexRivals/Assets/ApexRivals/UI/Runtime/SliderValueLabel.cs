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
            UguiViewText.Set(label, Format(value));
        }

        public static string Format(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
