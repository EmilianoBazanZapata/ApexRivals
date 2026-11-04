using UnityEngine;

namespace ApexRivals.RaceSession.Runtime
{
    public sealed class UnityTimeScaleController : ITimeScaleController
    {
        private float _previousTimeScale = 1f;
        private bool _paused;

        public float TimeScale => Time.timeScale;

        public void Pause()
        {
            if (_paused)
            {
                return;
            }

            _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _paused = true;
        }

        public void Resume()
        {
            // A newer positive decision must not be overwritten by an older
            // restoration (e.g. a destination already established its scale).
            if (Time.timeScale > 0f)
            {
                _paused = false;
                return;
            }

            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            _paused = false;
        }
    }
}
