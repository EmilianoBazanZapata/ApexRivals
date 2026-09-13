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
            if (!_paused && Time.timeScale > 0f)
            {
                return;
            }

            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            _paused = false;
        }
    }
}
