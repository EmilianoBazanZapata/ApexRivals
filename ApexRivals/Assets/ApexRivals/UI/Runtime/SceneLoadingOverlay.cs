using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SceneLoadingOverlay : MonoBehaviour, ISceneLoadingPresentation
    {
        [SerializeField, Min(0f), Tooltip("Unscaled seconds to show the overlay before content loading starts. Waiting here keeps the race countdown uncovered.")]
        private float preLoadDelaySeconds = 5f;

        private BootstrapUiInputSystem _input;
        private GameObject _surface;
        private RectTransform _spinner;
        private GameObject _previousSelection;
        private Coroutine _focusRecovery;
        public bool IsVisible => _surface != null && _surface.activeSelf;
        public float PreLoadDelaySeconds
        {
            get => preLoadDelaySeconds;
            set => preLoadDelaySeconds = Mathf.Max(0f, value);
        }

        public void Bind(BootstrapUiInputSystem input)
        {
            Hide(false);
            _input = input;
            if (_surface == null) Build();
        }

        public async Task ShowAsync()
        {
            if (!isActiveAndEnabled || _surface == null) return;
            if (_focusRecovery != null) StopCoroutine(_focusRecovery);
            _focusRecovery = null;
            _previousSelection = UiFocus.CurrentSelection;
            var readyAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, preLoadDelaySeconds);
            _surface.SetActive(true);
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
            if (_surface != null) _surface.SetActive(false);
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
            if (IsVisible) _spinner.Rotate(0f, 0f, -220f * Time.unscaledDeltaTime);
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
            if (_surface != null) Destroy(_surface);
        }

        private void Build()
        {
            _surface = new GameObject("SceneLoadingOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _surface.transform.SetParent(transform, false);
            var canvas = _surface.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            var scaler = _surface.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = new GameObject("OpaqueBackground", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(_surface.transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = ApexUiVisualStyle.Background;

            // Existing MainMenu wordmark treatment: built-in font, bold italic, white/red.
            Label("APEX", new Vector2(0f, 100f), new Vector2(650f, 140f), ApexUiVisualStyle.LogoPrimarySize, ApexUiVisualStyle.PrimaryText);
            Label("RIVALS", new Vector2(0f, 0f), new Vector2(650f, 70f), ApexUiVisualStyle.LogoAccentSize, ApexUiVisualStyle.PrimaryRed);
            _spinner = new GameObject("LoadingSpinner", typeof(RectTransform)).GetComponent<RectTransform>();
            _spinner.SetParent(_surface.transform, false);
            _spinner.anchoredPosition = new Vector2(0f, -150f);
            _spinner.sizeDelta = new Vector2(64f, 64f);
            for (var index = 0; index < 8; index++)
            {
                var dot = new GameObject("SpinnerSegment", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(_spinner, false);
                var dotRect = dot.GetComponent<RectTransform>();
                var angle = index * Mathf.PI / 4f;
                dotRect.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 25f;
                dotRect.sizeDelta = new Vector2(7f, 12f);
                dotRect.localRotation = Quaternion.Euler(0f, 0f, -index * 45f);
                var image = dot.GetComponent<Image>();
                var color = ApexUiVisualStyle.PrimaryRed;
                color.a = 0.2f + index * 0.1f;
                image.color = color;
                image.raycastTarget = false;
            }
            _surface.SetActive(false);
        }

        private void Label(string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var label = new GameObject(value, typeof(RectTransform), typeof(Text));
            label.transform.SetParent(_surface.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
        }
    }
}
