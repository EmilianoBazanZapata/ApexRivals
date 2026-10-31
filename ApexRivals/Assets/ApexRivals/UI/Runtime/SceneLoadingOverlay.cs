using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Opaque loading screen shown while content scenes load. The screen itself (canvas, wordmark, spinner) is the
    /// SceneLoadingOverlay prefab placed under this object in the Bootstrap scene and assigned here; this only
    /// shows it, spins the spinner and blocks UI input meanwhile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoadingOverlay : MonoBehaviour, ISceneLoadingPresentation
    {
        [SerializeField, Min(0f), Tooltip("Unscaled seconds to show the overlay before content loading starts. Waiting here keeps the race countdown uncovered.")]
        private float preLoadDelaySeconds = 5f;
        [SerializeField, Tooltip("The loading screen (inactive until shown).")] private GameObject surface;
        [SerializeField, Tooltip("Rotated while the screen is visible.")] private RectTransform spinner;
        [SerializeField, Tooltip("Spinner speed in degrees per second (negative turns clockwise).")] private float spinnerDegreesPerSecond = -220f;

        private BootstrapUiInputSystem _input;
        private GameObject _previousSelection;
        private Coroutine _focusRecovery;
        public bool IsVisible => surface != null && surface.activeSelf;
        public float PreLoadDelaySeconds
        {
            get => preLoadDelaySeconds;
            set => preLoadDelaySeconds = Mathf.Max(0f, value);
        }

        public void Bind(BootstrapUiInputSystem input)
        {
            Hide(false);
            _input = input;
            if (surface == null) Debug.LogWarning("Scene loading overlay has no loading screen assigned; loads will not be covered.", this);
        }

        /// <summary>Assigns the loading screen when the overlay is assembled in code (tests); scenes set it in the Inspector.</summary>
        public void UseSurface(GameObject loadingScreen, RectTransform loadingSpinner)
        {
            surface = loadingScreen;
            spinner = loadingSpinner;
            if (surface != null) surface.SetActive(false);
        }

        public async Task ShowAsync()
        {
            if (!isActiveAndEnabled || surface == null) return;
            if (_focusRecovery != null) StopCoroutine(_focusRecovery);
            _focusRecovery = null;
            _previousSelection = UiFocus.CurrentSelection;
            var readyAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, preLoadDelaySeconds);
            surface.SetActive(true);
            _input?.SetLoadingInputBlocked(this, true);
            UiFocus.Clear();
            Canvas.ForceUpdateCanvases();
            // Two frame boundaries guarantee one rendered opaque frame even when invoked
            // from UnitySynchronizationContext after Update.
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            // The requested visual pause happens before installation starts the race
            // countdown. A disabled/destroyed owner must not keep waiting for its timer.
            while (this != null && isActiveAndEnabled && IsVisible && Time.realtimeSinceStartupAsDouble < readyAt)
                await Awaitable.NextFrameAsync();
        }

        public void Hide(bool succeeded)
        {
            var wasVisible = IsVisible;
            if (surface != null) surface.SetActive(false);
            if (wasVisible && !succeeded && _input != null) _input.RestoreSceneFocusAfterLoading();
            if (_input != null) _input.SetLoadingInputBlocked(this, false);
            var previous = _previousSelection;
            _previousSelection = null;
            if (wasVisible && !succeeded && previous != null)
            {
                // Presenters re-enable failed commands after the navigation task returns.
                // Never restore outgoing focus after a successful scene swap.
                if (isActiveAndEnabled) _focusRecovery = StartCoroutine(RestoreFocus(previous));
                else UiFocus.Request(previous);
            }
        }

        private IEnumerator RestoreFocus(GameObject previous)
        {
            yield return null;
            if (!IsVisible && previous != null) UiFocus.Request(previous);
            _focusRecovery = null;
        }

        private void Update()
        {
            if (IsVisible && spinner != null) spinner.Rotate(0f, 0f, spinnerDegreesPerSecond * Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            if (_focusRecovery != null) StopCoroutine(_focusRecovery);
            _focusRecovery = null;
            Hide(false);
        }

        private void OnDestroy()
        {
            Hide(false);
        }
    }
}
