using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Engine sound for one car: its own <see cref="EngineSoundProfile"/> synthesised live by a
    /// <see cref="ProceduralEngineSynth"/>, so every model sounds like its engine. The car's rpm is mapped onto the
    /// profile's rev range (idle to redline sound the same share of the way). The load follows the throttle and drops
    /// out for a moment on every gear change; the strain grows with how fast the revs are climbing, so pulling through
    /// a gear sounds harder than cruising. 3D, routed through the Vehicles bus, silent while the game is paused and
    /// silent altogether without a profile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleEngineAudio : MonoBehaviour
    {
        private const float DefaultRedlineRpm = 6500f;
        private const float LoadAttack = 10f;
        private const float LoadRelease = 40f;
        private const float UpshiftCutSeconds = 0.2f;
        private const float DownshiftCutSeconds = 0.08f;
        // Share of the redline per second of rev climb that counts as full strain.
        private const float FullStrainRevRate = 0.2f;
        private const float RevRateResponse = 6f;
        private const float TipInSeconds = 0.35f;

        [SerializeField, Tooltip("The synth on this car's EngineSound child; its AudioSource is 3D and routed through the Vehicles bus.")]
        private ProceduralEngineSynth synth;

        private IVehicleTelemetry _telemetry;
        private WheelArcadeVehicleController _controller;
        private float _carRedlineRpm = DefaultRedlineRpm;
        private float _load;
        private float _shiftCut;
        private float _tipIn;
        private float _revRate;
        private float _previousRpm;
        private int _previousGear;
        private bool _wasOnThrottle;

        public EngineSoundProfile Profile => synth != null ? synth.Profile : null;

        private void Start()
        {
            _telemetry = GetComponentInChildren<IVehicleTelemetry>();
            _controller = GetComponentInChildren<WheelArcadeVehicleController>();
            if (synth == null || synth.Profile == null || _telemetry == null)
            {
                if (synth != null) synth.Running = false;
                enabled = false;
                return;
            }

            if (_telemetry is IVehicleTachometer tachometer && tachometer.RedlineRpm > 0f)
            {
                _carRedlineRpm = tachometer.RedlineRpm;
            }

            _previousGear = _telemetry.CurrentGear;
            _previousRpm = _telemetry.EngineRpm;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            // A showcase car (menu, garage) is not driven: its engine stays off.
            if (_controller == null || !_controller.isActiveAndEnabled)
            {
                synth.Running = false;
                return;
            }

            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var onThrottle = _controller.CurrentTorqueBeforeShiftSuppression > 0f;

            // Gear change: the clutch opens, so the engine goes off load for a moment - the audible break between gears.
            var gear = _telemetry.CurrentGear;
            if (gear != _previousGear)
            {
                _shiftCut = Mathf.Max(_shiftCut, gear > _previousGear ? UpshiftCutSeconds : DownshiftCutSeconds);
                _previousGear = gear;
            }

            _shiftCut = Mathf.Max(0f, _shiftCut - dt);
            var shifting = _shiftCut > 0f || _controller.ShiftInProgress;

            if (onThrottle && !_wasOnThrottle) _tipIn = TipInSeconds;
            _wasOnThrottle = onThrottle;
            _tipIn = Mathf.Max(0f, _tipIn - dt);

            // Traction control cutting power takes some of the load off too.
            var traction = _controller.CurrentTractionFactor;
            var targetLoad = onThrottle && !shifting ? Mathf.Lerp(0.6f, 1f, traction) : 0f;
            var loadRate = targetLoad > _load ? LoadAttack : LoadRelease;
            _load = Mathf.Lerp(_load, targetLoad, 1f - Mathf.Exp(-loadRate * dt));

            // Strain: how fast the revs are climbing (relative to the rev range), plus a kick when the throttle goes down.
            var carRpm = Mathf.Max(0f, _telemetry.EngineRpm);
            var rate = (carRpm - _previousRpm) / dt / Mathf.Max(1f, _carRedlineRpm);
            _previousRpm = carRpm;
            _revRate = Mathf.Lerp(_revRate, rate, 1f - Mathf.Exp(-RevRateResponse * dt));
            var climb = Mathf.Clamp01(_revRate / FullStrainRevRate);
            var strain = onThrottle && !shifting
                ? Mathf.Clamp01(0.3f + 0.7f * climb + _tipIn / TipInSeconds * 0.5f)
                : 0f;

            // The car's rev range is played on the engine's own: a high-revving engine screams where the car redlines.
            synth.Rpm = carRpm * synth.Profile.RedlineRpm / Mathf.Max(1f, _carRedlineRpm);
            synth.Throttle = _load;
            synth.Strain = strain;
            synth.Running = true;
        }
    }
}
