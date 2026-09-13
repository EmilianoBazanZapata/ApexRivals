using ApexRivals.Vehicle.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceHudUguiView : MonoBehaviour, IRaceHudView
    {
        [SerializeField] private Text countdownText;
        [SerializeField] private Text lapText;
        [SerializeField] private Text positionText;
        [SerializeField] private Text timeText;

        [Header("Vehicle Telemetry")]
        [SerializeField] private Text speedValueText;
        [SerializeField] private Text gearValueText;
        [SerializeField] private Text rpmValueText;

        private RaceHudPresenter _presenter;
        private IVehicleTelemetry _telemetry;
        private bool _hasRenderedStaticValues;
        private bool _hasRenderedTelemetry;
        private CountdownDisplayState _lastCountdownState;
        private int _lastCountdownValue;
        private int _lastCurrentLap;
        private int _lastTotalLaps;
        private int _lastCurrentPosition;
        private int _lastParticipantCount;
        private int _lastDisplaySpeedKph;
        private int _lastDisplayGear;
        private bool _lastDisplayIsReversing;
        private int _lastDisplayRpm;

        public void Bind(RaceHudPresenter presenter)
        {
            _presenter = presenter;
            _hasRenderedStaticValues = false;
        }

        /// <summary>
        /// Caches the player's telemetry source (resolved once by RaceUiSceneInstaller
        /// during race setup - never re-resolved per frame). Pass null to stop
        /// rendering (e.g. on session teardown).
        /// </summary>
        public void BindTelemetry(IVehicleTelemetry telemetry)
        {
            _telemetry = telemetry;
            _hasRenderedTelemetry = false;
        }

        public void Render(RaceHudViewModel viewModel)
        {
            RenderCountdown(viewModel);
            RenderLap(viewModel);
            RenderPosition(viewModel);
            UguiViewText.Set(timeText, UguiViewText.FormatTime(viewModel.ElapsedRaceTime));
        }

        private void Update()
        {
            _presenter?.UpdateElapsedTimeSnapshot();
            RenderTelemetry();
        }

        // Speed/RPM change continuously (every physics step), so they are refreshed
        // every frame like the elapsed race time above - unlike the discrete,
        // event-driven race progress fields (lap/position/countdown). Each value is
        // only formatted and written to its Text when the rounded/displayed value
        // actually changes, avoiding per-frame string churn (mirrors the Render*
        // methods above).
        private void RenderTelemetry()
        {
            if (_telemetry == null)
            {
                return;
            }

            var speedKph = VehicleTelemetryPresentation.RoundSpeedKph(_telemetry.SpeedKph);
            if (!_hasRenderedTelemetry || _lastDisplaySpeedKph != speedKph)
            {
                UguiViewText.Set(speedValueText, VehicleTelemetryPresentation.FormatSpeedKph(speedKph));
                _lastDisplaySpeedKph = speedKph;
            }

            var currentGear = _telemetry.CurrentGear;
            var isReversing = _telemetry.IsReversing;
            if (!_hasRenderedTelemetry || _lastDisplayGear != currentGear || _lastDisplayIsReversing != isReversing)
            {
                UguiViewText.Set(gearValueText, VehicleTelemetryPresentation.FormatGear(currentGear, isReversing));
                _lastDisplayGear = currentGear;
                _lastDisplayIsReversing = isReversing;
            }

            var rpm = VehicleTelemetryPresentation.RoundRpm(_telemetry.EngineRpm);
            if (!_hasRenderedTelemetry || _lastDisplayRpm != rpm)
            {
                UguiViewText.Set(rpmValueText, VehicleTelemetryPresentation.FormatRpm(rpm));
                _lastDisplayRpm = rpm;
            }

            _hasRenderedTelemetry = true;
        }

        private void RenderCountdown(RaceHudViewModel viewModel)
        {
            var countdownValue = viewModel.CountdownState == CountdownDisplayState.Hidden
                ? 0
                : Mathf.CeilToInt(viewModel.CountdownValue);
            if (_hasRenderedStaticValues
                && _lastCountdownState == viewModel.CountdownState
                && _lastCountdownValue == countdownValue)
            {
                return;
            }

            UguiViewText.Set(
                countdownText,
                viewModel.CountdownState == CountdownDisplayState.Hidden ? string.Empty : countdownValue.ToString());
            _lastCountdownState = viewModel.CountdownState;
            _lastCountdownValue = countdownValue;
        }

        private void RenderLap(RaceHudViewModel viewModel)
        {
            if (_hasRenderedStaticValues
                && _lastCurrentLap == viewModel.CurrentLap
                && _lastTotalLaps == viewModel.TotalLaps)
            {
                return;
            }

            UguiViewText.Set(lapText, $"Lap {viewModel.CurrentLap}/{viewModel.TotalLaps}");
            _lastCurrentLap = viewModel.CurrentLap;
            _lastTotalLaps = viewModel.TotalLaps;
        }

        private void RenderPosition(RaceHudViewModel viewModel)
        {
            if (_hasRenderedStaticValues
                && _lastCurrentPosition == viewModel.CurrentPosition
                && _lastParticipantCount == viewModel.ParticipantCount)
            {
                return;
            }

            UguiViewText.Set(positionText, $"Position {viewModel.CurrentPosition}/{viewModel.ParticipantCount}");
            _lastCurrentPosition = viewModel.CurrentPosition;
            _lastParticipantCount = viewModel.ParticipantCount;
            _hasRenderedStaticValues = true;
        }
    }
}
