using ApexRivals.Vehicle.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApexRivals.VFX.Runtime
{
    /// <summary>
    /// Boost feedback through post processing: blends a global Volume (chromatic aberration, lens distortion, a red
    /// vignette and motion blur) in while the player's nitro is firing and back out when it stops. Follows the
    /// Settings post-processing switch automatically, since the camera then skips every Volume.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class NitroPostProcessEffect : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("Seconds to reach full strength when the boost starts.")] private float fadeInSeconds = 0.15f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to fade away after the boost ends.")] private float fadeOutSeconds = 0.6f;
        [SerializeField, Min(0.1f), Tooltip("Seconds between searches for the player's nitro while none is bound.")] private float searchInterval = 0.5f;

        private Volume _volume;
        private PlayerVehicleNitro _nitro;
        private float _nextSearch;

        private void Awake()
        {
            _volume = GetComponent<Volume>();
            _volume.weight = 0f;
        }

        private void Update()
        {
            if (_nitro == null || !_nitro.isActiveAndEnabled)
            {
                _nitro = null;
                if (Time.unscaledTime >= _nextSearch)
                {
                    _nextSearch = Time.unscaledTime + searchInterval;
                    // Rivals carry the component too but keep it disabled; only the player's is enabled.
                    foreach (var candidate in Object.FindObjectsByType<PlayerVehicleNitro>(FindObjectsSortMode.None))
                    {
                        if (candidate.isActiveAndEnabled) { _nitro = candidate; break; }
                    }
                }
            }

            var target = _nitro != null && _nitro.IsBoosting ? 1f : 0f;
            var rate = target > _volume.weight ? 1f / fadeInSeconds : 1f / fadeOutSeconds;
            _volume.weight = Mathf.MoveTowards(_volume.weight, target, rate * Time.deltaTime);
        }
    }
}
