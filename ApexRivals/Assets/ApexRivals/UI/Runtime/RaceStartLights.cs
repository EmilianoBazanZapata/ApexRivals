using ApexRivals.Audio.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Presentation only. RaceCoordinator owns countdown time and the driving gates.</summary>
    [DisallowMultipleComponent]
    public sealed class RaceStartLights : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private RaceCoordinator coordinator;
        [SerializeField] private RaceSessionSceneInstaller sessionInstaller;
        [SerializeField] private Text legacyCountdown;
        [SerializeField] private Canvas display;
        [SerializeField] private Animator animator;
        [SerializeField] private Text label;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip countdownCue;
        [SerializeField] private AudioClip goCue;
        [SerializeField, Range(0f, 1f)] private float cueVolume = 0.7f;
        private RaceSessionCoordinator _session;
        private bool _legacyEnabled;
        private bool _paused;
        private bool _showing;
        private static readonly int[] States =
        {
            Animator.StringToHash("Base Layer.Idle"), Animator.StringToHash("Base Layer.Go"),
            Animator.StringToHash("Base Layer.One"), Animator.StringToHash("Base Layer.Two"),
            Animator.StringToHash("Base Layer.Three")
        };

        // 0 hidden, 3/2/1 countdown, -1 GO. Exposed for scene diagnostics.
        public int Stage { get; private set; }
        public int CueCount { get; private set; }
        public bool IsVisible => display != null && display.enabled;
        public Animator Animator => animator;
        public AudioSource AudioSource => audioSource;
        public Text Label => label;

        public void Install(ApplicationContext context)
        {
            Uninstall();
            if (coordinator == null || sessionInstaller == null || sessionInstaller.Session == null ||
                display == null || animator == null || animator.runtimeAnimatorController == null ||
                label == null || audioSource == null || countdownCue == null || goCue == null)
            {
                Debug.LogWarning("Start lights are not configured; keeping the HUD countdown.", this);
                return;
            }
            _session = sessionInstaller.Session;
            if (legacyCountdown != null) { _legacyEnabled = legacyCountdown.enabled; legacyCountdown.enabled = false; }
            CueCount = 0;
            coordinator.CountdownChanged += OnCountdown;
            coordinator.RaceStarted += OnStarted;
            _session.StateChanged += OnSessionChanged;
            // Without an introduction, StartCountdown can precede this installer's subscription.
            if (coordinator.State == RaceState.Countdown) ShowCountdown(coordinator.CountdownRemaining);
        }

        private void OnCountdown(CountdownChangedEvent change) => ShowCountdown(change.RemainingSeconds);
        private void ShowCountdown(float remaining)
        {
            if (remaining <= 0f) return; // Only RaceStarted is allowed to display GO.
            Show(Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 3));
        }
        private void OnStarted(RaceStartedEvent change) => Show(-1);

        private void Show(int stage)
        {
            if (Stage == stage) return; // CountdownChanged is raised every frame.
            Stage = stage;
            _showing = true;
            display.enabled = !_paused;
            label.text = stage < 0 ? "¡YA!" : stage.ToString();
            animator.enabled = true;
            animator.Play(States[stage < 0 ? 1 : stage + 1], 0, 0f);
            animator.Update(0f); // Sample the lamps in the same frame as the cue.
            audioSource.volume = cueVolume * (GameAudio.Current?.Mixer.GetVolume(AudioBus.Ui) ?? 1f);
            audioSource.clip = stage < 0 ? goCue : countdownCue;
            audioSource.Play();
            if (_paused) audioSource.Pause();
            CueCount++;
        }

        private void Update()
        {
            if (_session == null) return;
            audioSource.volume = cueVolume * (GameAudio.Current?.Mixer.GetVolume(AudioBus.Ui) ?? 1f);
            if (Stage == -1 && !_paused && animator.enabled && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                Hide();
        }

        private void OnSessionChanged(RaceSessionLifecycleState previous, RaceSessionLifecycleState current)
        {
            _paused = current == RaceSessionLifecycleState.Paused;
            animator.speed = _paused ? 0f : 1f;
            display.enabled = _showing && !_paused;
            if (_paused) audioSource.Pause();
            else if (previous == RaceSessionLifecycleState.Paused) audioSource.UnPause();
            if (current == RaceSessionLifecycleState.Retrying || current == RaceSessionLifecycleState.Exiting ||
                current == RaceSessionLifecycleState.ShowingResults || current == RaceSessionLifecycleState.Disposed)
            {
                Hide();
                audioSource.Stop();
            }
        }

        private void Hide()
        {
            _showing = false;
            if (display != null) display.enabled = false;
            if (animator != null) animator.enabled = false;
        }

        public void Uninstall()
        {
            if (_session != null)
            {
                if (coordinator != null) { coordinator.CountdownChanged -= OnCountdown; coordinator.RaceStarted -= OnStarted; }
                _session.StateChanged -= OnSessionChanged;
                if (legacyCountdown != null) legacyCountdown.enabled = _legacyEnabled;
            }
            _session = null;
            _paused = false;
            Stage = 0;
            Hide();
            if (animator != null) animator.speed = 1f;
            if (audioSource != null) audioSource.Stop();
        }

        private void OnDestroy() => Uninstall();
    }
}
