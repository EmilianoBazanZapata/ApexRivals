using ApexRivals.Vehicle.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceHudUguiView : MonoBehaviour, IRaceHudView
    {
        [Header("Tachometer configuration")]
        [SerializeField, Tooltip("Disable to preserve the gauge's Major Ticks and Red Zone Start values.")] private bool autoConfigureTachometer = true;
        [SerializeField, Min(1f), Tooltip("Scale used when automatic configuration is disabled.")] private float manualTachometerScaleRpm = 7000f;
        [SerializeField, Min(1f)] private float tachometerTickStepRpm = 1000f;
        [SerializeField, Range(1, 30)] private int maximumTachometerTicks = 12;
        [SerializeField, Range(0f, 1f)] private float redZoneRedlineRatio = 0.85f;
        [SerializeField, Tooltip("Disable to preserve each label's text and visibility.")] private bool autoSetTachometerLabels = true;
        [SerializeField, Tooltip("Disable to preserve each label's RectTransform position.")] private bool autoLayoutTachometerLabels = true;
        [SerializeField] private float tachometerLabelRadiusOffset = -34f;
        [SerializeField] private Vector2 tachometerLabelOffset;

        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private Text countdownText;
        [SerializeField] private Text lapText;
        [SerializeField] private Text positionText;
        [SerializeField] private Text timeText;

        [Header("Vehicle Telemetry")]
        [SerializeField] private Text speedValueText;
        [SerializeField] private Text gearValueText;
        [SerializeField] private Text rpmValueText;
        [SerializeField] private NitroHudUguiView nitroView;

        [Header("Position / lap banner (optional)")]
        [SerializeField, Tooltip("Big position number (\"2\"); replaces positionText when assigned.")] private Text positionNumberText;
        [SerializeField, Tooltip("Ordinal suffix (\"ND\").")] private Text positionSuffixText;
        [SerializeField, Tooltip("Field size (\"/3\").")] private Text positionTotalText;
        [SerializeField, Tooltip("Lap counter (\"2/3\"); replaces lapText when assigned.")] private Text lapValueText;
        [SerializeField] private ChevronProgressBar raceProgressBar;

        [Header("Lap timing (optional)")]
        [SerializeField] private Text currentLapText;
        [SerializeField] private Text bestLapText;

        [Header("Standings tower (optional)")]
        [SerializeField] private GameObject[] standingRows;
        [SerializeField] private Graphic[] standingRowBackgrounds;
        [SerializeField] private Text[] standingPositionTexts;
        [SerializeField] private Text[] standingNameTexts;
        [SerializeField] private Text[] standingGapTexts;

        [Header("Tachometer (optional)")]
        [SerializeField] private ArcGaugeGraphic tachometer;
        [SerializeField, Tooltip("Scale labels (x1000 rpm), placed along the arc when telemetry is bound.")] private Text[] tachometerLabels;

        private const float StandingsRefreshInterval = 0.2f;
        private const float DefaultRedlineRpm = 7000f;

        private readonly System.Collections.Generic.List<RaceStandingEntry> _standings = new System.Collections.Generic.List<RaceStandingEntry>();
        private RaceStandingsTracker _standingsTracker;
        private float _standingsTimer;
        private int _lastCurrentLapCentis = -1;
        private int _lastBestLapCentis = -2;
        private float _tachometerScaleRpm = DefaultRedlineRpm;

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
            ConfigureTachometer(telemetry as IVehicleTachometer);
        }

        /// <summary>Lap times, race progress and the standings tower; pass null on session teardown.</summary>
        public void BindStandings(RaceStandingsTracker tracker)
        {
            _standingsTracker = tracker;
            _standingsTimer = 0f;
            _lastCurrentLapCentis = -1;
            _lastBestLapCentis = -2;
        }

        public void BindNitro(PlayerVehicleNitro nitro)
        {
            if (nitroView != null) nitroView.Bind(nitro);
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
            _presenter?.UpdateContinuousSnapshot();
            RenderTelemetry();
            RenderTiming();
        }

        private void RenderTiming()
        {
            if (_standingsTracker == null)
            {
                return;
            }

            // Hundredths are enough to decide whether the text changed; the label still shows milliseconds.
            var current = _standingsTracker.CurrentLapTime;
            var currentCentis = Mathf.FloorToInt(current * 100f);
            if (currentCentis != _lastCurrentLapCentis)
            {
                UguiViewText.Set(currentLapText, UguiViewText.FormatTime(current));
                _lastCurrentLapCentis = currentCentis;
            }

            var best = _standingsTracker.BestLapTime;
            var bestCentis = best < 0f ? -1 : Mathf.FloorToInt(best * 100f);
            if (bestCentis != _lastBestLapCentis)
            {
                UguiViewText.Set(bestLapText, best < 0f ? "--:--.---" : UguiViewText.FormatTime(best));
                _lastBestLapCentis = bestCentis;
            }

            if (raceProgressBar != null)
            {
                raceProgressBar.Value = _standingsTracker.RaceProgress;
            }

            _standingsTimer -= Time.unscaledDeltaTime;
            if (_standingsTimer > 0f)
            {
                return;
            }

            _standingsTimer = StandingsRefreshInterval;
            RenderStandings();
        }

        private void RenderStandings()
        {
            if (standingRows == null || standingRows.Length == 0)
            {
                return;
            }

            _standingsTracker.GetStandings(_standings);
            for (var row = 0; row < standingRows.Length; row++)
            {
                var visible = row < _standings.Count;
                if (standingRows[row] != null && standingRows[row].activeSelf != visible)
                {
                    standingRows[row].SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                var entry = _standings[row];
                UguiViewText.Set(At(standingPositionTexts, row), entry.Position.ToString(System.Globalization.CultureInfo.InvariantCulture));
                UguiViewText.Set(At(standingNameTexts, row), entry.Name);
                UguiViewText.Set(At(standingGapTexts, row), entry.Gap);
                // The player's row is filled red, the rivals sit on the dark panel.
                var background = At(standingRowBackgrounds, row);
                if (background != null)
                {
                    background.color = entry.IsPlayer ? Palette.PrimaryRed : Palette.PanelDeep;
                }
            }
        }

        /// <summary>1ST, 2ND, 3RD, 4TH ... 11TH, 12TH, 13TH, 21ST.</summary>
        public static string OrdinalSuffix(int position)
        {
            if (position <= 0) return string.Empty;
            var lastTwo = position % 100;
            if (lastTwo >= 11 && lastTwo <= 13) return "TH";
            switch (position % 10)
            {
                case 1: return "ST";
                case 2: return "ND";
                case 3: return "RD";
                default: return "TH";
            }
        }

        private static T At<T>(T[] items, int index) where T : class
        {
            return items != null && index < items.Length ? items[index] : null;
        }

        /// <summary>Scales the arc to the car's redline (labels in thousands of rpm) and marks the red zone.</summary>
        private void ConfigureTachometer(IVehicleTachometer source)
        {
            if (tachometer == null)
            {
                return;
            }

            var redline = source != null && source.RedlineRpm > 0f ? source.RedlineRpm : DefaultRedlineRpm;
            var stepRpm = Mathf.Max(1f, tachometerTickStepRpm);
            var steps = autoConfigureTachometer
                ? Mathf.Clamp(Mathf.CeilToInt(redline / stepRpm), 1, Mathf.Max(1, maximumTachometerTicks))
                : Mathf.Max(1, tachometer.MajorTicks);
            _tachometerScaleRpm = autoConfigureTachometer ? steps * stepRpm : Mathf.Max(1f, manualTachometerScaleRpm);
            if (autoConfigureTachometer)
            {
                tachometer.MajorTicks = steps;
                tachometer.RedZoneStart = Mathf.Clamp01(redline * redZoneRedlineRatio / _tachometerScaleRpm);
            }
            if (tachometerLabels == null)
            {
                return;
            }

            for (var index = 0; index < tachometerLabels.Length; index++)
            {
                var label = tachometerLabels[index];
                if (label == null) continue;
                var visible = index <= steps;
                if (autoSetTachometerLabels) label.gameObject.SetActive(visible);
                if (!visible) continue;
                if (autoSetTachometerLabels) UguiViewText.Set(label, (index * _tachometerScaleRpm / steps / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                // Labels are children of the gauge (centred anchors and pivot) and sit just inside the ring.
                if (autoLayoutTachometerLabels) label.rectTransform.anchoredPosition = tachometer.PointAt(index / (float)steps, tachometerLabelRadiusOffset) + tachometerLabelOffset;
            }
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
                // The tachometer layout prints the unit in its own label, so the readout is just the number.
                UguiViewText.Set(speedValueText, tachometer != null
                    ? speedKph.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : VehicleTelemetryPresentation.FormatSpeedKph(speedKph));
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
                if (tachometer != null)
                {
                    tachometer.Value = rpm / _tachometerScaleRpm;
                }

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
            UguiViewText.Set(lapValueText, $"{viewModel.CurrentLap}/{viewModel.TotalLaps}");
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

            UguiViewText.Set(positionText, $"{viewModel.CurrentPosition}/{viewModel.ParticipantCount}");
            UguiViewText.Set(positionNumberText, viewModel.CurrentPosition.ToString(System.Globalization.CultureInfo.InvariantCulture));
            UguiViewText.Set(positionSuffixText, OrdinalSuffix(viewModel.CurrentPosition));
            UguiViewText.Set(positionTotalText, "/" + viewModel.ParticipantCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _lastCurrentPosition = viewModel.CurrentPosition;
            _lastParticipantCount = viewModel.ParticipantCount;
            _hasRenderedStaticValues = true;
        }
    }
}
