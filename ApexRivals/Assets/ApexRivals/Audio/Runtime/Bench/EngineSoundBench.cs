using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ApexRivals.Audio.Runtime.Bench
{
    /// <summary>
    /// Development bench to audition <see cref="EngineSoundProfile"/>s: a tiny free-revving engine (throttle, brake,
    /// neutral, ignition, rev limiter) drives a <see cref="ProceduralEngineSynth"/>, with readouts, the firing order,
    /// an oscilloscope and a spectrum. Keys: W/↑ throttle, S/↓ brake, N neutral, E engine on/off, 1-9 engine.
    /// </summary>
    public sealed class EngineSoundBench : MonoBehaviour
    {
        [Header("Cylinder lamp colours")]
        [SerializeField] private Color firingLampColor = new Color(0.9f, 0.1f, 0.08f);
        [SerializeField] private Color restingLampColor = new Color(0.25f, 0.27f, 0.3f);
        [SerializeField, Tooltip("Keep each Image's authored colour as a tint for the animated state colours.")] private bool multiplyAuthoredLampColor;
        private readonly System.Collections.Generic.Dictionary<Image, Color> _lampColors = new System.Collections.Generic.Dictionary<Image, Color>();

        private const int SpectrumBins = 64;
        private const float LimiterCut = 0.06f;

        [SerializeField] private ProceduralEngineSynth synth;
        [SerializeField] private EngineSoundProfile[] profiles = new EngineSoundProfile[0];

        [Header("UI")]
        [SerializeField] private Button[] engineButtons = new Button[0];
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private RectTransform revsFill;
        [SerializeField] private RectTransform throttleFill;
        [SerializeField] private Text rpmText;
        [SerializeField] private Text gearText;
        [SerializeField] private Text loadText;
        [SerializeField] private Text firingText;
        [SerializeField] private Text boostText;
        [SerializeField] private Text firingOrderText;
        [SerializeField] private Image[] cylinderLamps = new Image[0];
        [SerializeField] private Text[] cylinderLabels = new Text[0];
        [SerializeField] private SignalGraphic scope;
        [SerializeField] private SignalGraphic spectrum;
        [SerializeField] private HoldButton throttleButton;
        [SerializeField] private HoldButton brakeButton;
        [SerializeField] private Button neutralButton;
        [SerializeField] private Text neutralLabel;
        [SerializeField] private Button ignitionButton;
        [SerializeField] private Text ignitionLabel;
        [SerializeField] private Slider throttleAmount;

        private readonly float[] _signal = new float[ProceduralEngineSynth.ScopeLength];
        private readonly float[] _scope = new float[512];
        private readonly float[] _spectrum = new float[SpectrumBins];
        private int _selected;
        private float _rpm;
        private bool _running = true;
        private bool _neutral = true;
        private float _limiterTimer;
        private float _appliedThrottle;

        private void Start()
        {
            for (var i = 0; i < engineButtons.Length; i++)
            {
                var index = i;
                if (engineButtons[i] != null) engineButtons[i].onClick.AddListener(() => Select(index));
            }

            if (neutralButton != null) neutralButton.onClick.AddListener(() => _neutral = !_neutral);
            if (ignitionButton != null) ignitionButton.onClick.AddListener(() => _running = !_running);
            Select(0);
        }

        private void Select(int index)
        {
            if (index < 0 || index >= profiles.Length || profiles[index] == null) return;
            _selected = index;
            var profile = profiles[index];
            synth.SetProfile(profile);
            _rpm = profile.IdleRpm;
            if (titleText != null) titleText.text = profile.DisplayName;
            if (descriptionText != null) descriptionText.text = profile.Description;
            if (firingOrderText != null) firingOrderText.text = "FIRING ORDER  " + string.Join("-", profile.FiringOrder);
            for (var i = 0; i < cylinderLamps.Length; i++)
            {
                var visible = i < profile.FiringOrder.Length;
                cylinderLamps[i].gameObject.SetActive(visible);
                if (visible && i < cylinderLabels.Length) cylinderLabels[i].text = profile.FiringOrder[i].ToString(CultureInfo.InvariantCulture);
            }

            for (var i = 0; i < engineButtons.Length; i++)
            {
                var visual = engineButtons[i] != null ? engineButtons[i].GetComponent<ApexRivals.UI.Runtime.UguiMenuButtonVisualState>() : null;
                if (visual != null) visual.IsCurrent = i == index;
            }
        }

        private void Update()
        {
            ReadKeys();
            var profile = profiles.Length > 0 ? profiles[_selected] : null;
            if (profile == null || synth == null) return;

            var keyboard = Keyboard.current;
            var throttleHeld = (throttleButton != null && throttleButton.Held) || (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed));
            var brakeHeld = (brakeButton != null && brakeButton.Held) || (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed));
            var amount = throttleAmount != null ? throttleAmount.value : 1f;
            Simulate(profile, throttleHeld ? amount : 0f, brakeHeld, Time.deltaTime);

            synth.Rpm = _rpm;
            synth.Throttle = _appliedThrottle;
            synth.Strain = _appliedThrottle;
            synth.Running = _running;
            RenderUi(profile);
        }

        private void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.nKey.wasPressedThisFrame) _neutral = !_neutral;
            if (keyboard.eKey.wasPressedThisFrame) _running = !_running;
            for (var i = 0; i < 9; i++)
            {
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Select(i);
            }
        }

        /// <summary>Free-revving engine: throttle against friction, an idle governor, a fuel-cut rev limiter.</summary>
        private void Simulate(EngineSoundProfile profile, float throttle, bool brake, float dt)
        {
            var redline = profile.RedlineRpm;
            var idle = profile.IdleRpm;
            if (!_running)
            {
                _appliedThrottle = 0f;
                _rpm = Mathf.MoveTowards(_rpm, 0f, redline * 0.5f * dt);
                return;
            }

            // Rev limiter: at the redline the fuel is cut for a moment, which makes the engine bounce off it.
            if (_rpm >= redline) _limiterTimer = LimiterCut;
            _limiterTimer -= dt;
            var effective = _limiterTimer > 0f ? 0f : throttle;
            // The idle governor keeps the engine from stalling.
            if (_rpm < idle) effective = Mathf.Max(effective, Mathf.Clamp01((idle - _rpm) / 200f) * 0.35f);
            _appliedThrottle = effective;

            var share = _rpm / redline;
            var torque = effective * (1.15f - 0.35f * share);
            var friction = 0.12f + 0.3f * share + (brake && !_neutral ? 0.8f : 0f);
            // In gear the car's mass slows the revs down.
            var inertia = _neutral ? 1f : 5f;
            _rpm += (torque - friction) * redline * 1.8f / inertia * dt;
            _rpm = Mathf.Clamp(_rpm, idle * 0.85f, redline * 1.01f);
        }

        private void RenderUi(EngineSoundProfile profile)
        {
            SetFill(revsFill, _rpm / profile.RedlineRpm);
            SetFill(throttleFill, _appliedThrottle);
            Set(rpmText, Mathf.RoundToInt(_rpm).ToString(CultureInfo.InvariantCulture));
            Set(gearText, _neutral ? "N" : "3");
            Set(loadText, Mathf.RoundToInt(_appliedThrottle * 100f) + "%");
            Set(firingText, Mathf.RoundToInt(profile.FiringFrequency(_rpm)) + " Hz");
            Set(boostText, profile.IsTurbo ? (_appliedThrottle * _rpm / profile.RedlineRpm).ToString("0.0", CultureInfo.InvariantCulture) + " bar" : "-");
            Set(neutralLabel, _neutral ? "IN GEAR" : "NEUTRAL");
            Set(ignitionLabel, _running ? "ENGINE OFF" : "ENGINE ON");

            var last = synth.LastFiringIndex;
            for (var i = 0; i < cylinderLamps.Length; i++)
            {
                if (cylinderLamps[i] != null && cylinderLamps[i].gameObject.activeSelf)
                {
                    var lamp = cylinderLamps[i];
                    if (!_lampColors.TryGetValue(lamp, out var authoredColor))
                    {
                        authoredColor = lamp.color;
                        _lampColors.Add(lamp, authoredColor);
                    }
                    var stateColor = _running && i == last ? firingLampColor : restingLampColor;
                    lamp.color = multiplyAuthoredLampColor ? authoredColor * stateColor : stateColor;
                }
            }

            synth.CopyScope(_signal);
            if (scope != null)
            {
                // The last 512 samples, scaled so the waveform fills the panel at any level.
                var peak = 0.05f;
                for (var i = _signal.Length - _scope.Length; i < _signal.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(_signal[i]));
                for (var i = 0; i < _scope.Length; i++) _scope[i] = _signal[_signal.Length - _scope.Length + i] / peak * 0.9f;
                scope.SetValues(_scope);
            }

            if (spectrum != null)
            {
                // The synth writes its samples itself, which Unity's spectrum analysis does not see, so the bench
                // measures them: Goertzel filters at log-spaced frequencies from 40 Hz to 10 kHz, Hann-windowed.
                var rate = AudioSettings.outputSampleRate;
                var length = _signal.Length;
                for (var b = 0; b < SpectrumBins; b++)
                {
                    var frequency = 40f * Mathf.Pow(250f, (b + 0.5f) / SpectrumBins);
                    var coefficient = 2f * Mathf.Cos(2f * Mathf.PI * frequency / rate);
                    float s1 = 0f, s2 = 0f;
                    for (var i = 0; i < length; i++)
                    {
                        var window = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (length - 1));
                        var s0 = _signal[i] * window + coefficient * s1 - s2;
                        s2 = s1;
                        s1 = s0;
                    }

                    var magnitude = Mathf.Sqrt(Mathf.Max(0f, s1 * s1 + s2 * s2 - coefficient * s1 * s2)) / (length * 0.25f);
                    var db = 20f * Mathf.Log10(magnitude + 1e-7f);
                    _spectrum[b] = Mathf.Lerp(_spectrum[b], Mathf.InverseLerp(-80f, -10f, db), 0.5f);
                }

                spectrum.SetValues(_spectrum);
            }
        }

        private static void SetFill(RectTransform fill, float share)
        {
            if (fill == null) return;
            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(share);
            fill.anchorMax = max;
        }

        private static void Set(Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }
    }
}
