using System.Collections.Generic;
using ApexRivals.Audio.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// "Now playing" banner: fades in with the soundtrack track's name each time <see cref="GameAudio"/> starts one,
    /// stays a few seconds and fades out. It sits in the first of its candidate slots that no visible screen UI
    /// covers, so it never lands on menus or the race HUD. Runs on unscaled time so it also works over a paused race.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NowPlayingUguiView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField, Tooltip("The banner; moved between the slots and up into place while fading in.")] private RectTransform panel;
        [SerializeField] private Text titleText;
        [SerializeField, Tooltip("Banner centres in the parent frame, in order of preference.")]
        private Vector2[] slots = { new Vector2(0f, 488f), new Vector2(0f, -400f), new Vector2(0f, 380f), new Vector2(-600f, 0f), new Vector2(600f, 0f) };
        [SerializeField, Min(0f)] private float visibleSeconds = 5f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;
        [SerializeField] private float slideDistance = 20f;
        [SerializeField, Min(0.05f), Tooltip("Seconds between checks that the slot is still clear while the banner shows.")] private float placementInterval = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Graphics covering more of the screen than this (backdrops, dimmers) do not block a slot.")]
        private float backdropScreenFraction = 0.35f;

        private readonly List<Rect> _blockers = new List<Rect>();
        private readonly Vector3[] _corners = new Vector3[4];
        private GameAudio _audio;
        private Canvas _canvas;
        private Vector2 _restPosition;
        private float _shownAt = float.NegativeInfinity;
        private float _nextPlacement;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (panel != null) _restPosition = panel.anchoredPosition;
            SetVisibility(0f);
        }

        private void OnDisable()
        {
            if (_audio != null) _audio.TrackStarted -= Show;
            _audio = null;
        }

        private void Update()
        {
            // GameAudio may come up after this view; a track that already started is still announced.
            if (_audio == null && GameAudio.Current != null)
            {
                _audio = GameAudio.Current;
                _audio.TrackStarted += Show;
                if (!string.IsNullOrEmpty(_audio.CurrentTrackName)) Show(_audio.CurrentTrackName);
            }

            var elapsed = Time.unscaledTime - _shownAt;
            var fadeIn = Mathf.Clamp01(elapsed / fadeSeconds);
            var fadeOut = Mathf.Clamp01((visibleSeconds + fadeSeconds - elapsed) / fadeSeconds);
            var visibility = Mathf.Min(fadeIn, fadeOut);

            // Screens can change under the banner (menu to garage, race to results): keep it on a clear slot.
            if (visibility > 0f && Time.unscaledTime >= _nextPlacement)
            {
                _nextPlacement = Time.unscaledTime + placementInterval;
                Place();
            }

            SetVisibility(visibility);
        }

        public void Show(string trackName)
        {
            UguiViewText.Set(titleText, string.IsNullOrEmpty(trackName) ? string.Empty : trackName.ToUpperInvariant());
            _shownAt = Time.unscaledTime;
            _nextPlacement = 0f;
        }

        private void Place()
        {
            if (panel == null || slots == null || slots.Length == 0) return;
            CollectBlockers();
            var current = Overlap(_restPosition);
            if (current <= 0f) return;
            var best = _restPosition;
            var bestOverlap = current;
            foreach (var slot in slots)
            {
                var overlap = Overlap(slot);
                if (overlap < bestOverlap)
                {
                    best = slot;
                    bestOverlap = overlap;
                }

                if (overlap <= 0f) break;
            }

            _restPosition = best;
        }

        /// <summary>Screen rects of every visible graphic on the other screen-space canvases.</summary>
        private void CollectBlockers()
        {
            _blockers.Clear();
            var screenArea = (float)Screen.width * Screen.height;
            foreach (var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                if (!graphic.isActiveAndEnabled || graphic.canvas == null || graphic.canvas.rootCanvas == _canvas.rootCanvas) continue;
                var root = graphic.canvas.rootCanvas;
                if (root.renderMode == RenderMode.WorldSpace) continue;
                if (graphic.color.a <= 0.05f || graphic.canvasRenderer.GetInheritedAlpha() <= 0.05f) continue;
                if (graphic is Text text && string.IsNullOrEmpty(text.text)) continue;
                var rect = ScreenRect(graphic.rectTransform, root);
                if (rect.width * rect.height > screenArea * backdropScreenFraction) continue;
                _blockers.Add(rect);
            }
        }

        private float Overlap(Vector2 slot)
        {
            var previous = panel.anchoredPosition;
            panel.anchoredPosition = slot;
            var mine = ScreenRect(panel, _canvas.rootCanvas);
            panel.anchoredPosition = previous;
            var total = 0f;
            foreach (var other in _blockers)
            {
                var width = Mathf.Min(mine.xMax, other.xMax) - Mathf.Max(mine.xMin, other.xMin);
                var height = Mathf.Min(mine.yMax, other.yMax) - Mathf.Max(mine.yMin, other.yMin);
                if (width > 0f && height > 0f) total += width * height;
            }

            return total;
        }

        private Rect ScreenRect(RectTransform target, Canvas root)
        {
            target.GetWorldCorners(_corners);
            var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            var min = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(camera, _corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private void SetVisibility(float visibility)
        {
            if (group != null) group.alpha = visibility;
            if (panel != null) panel.anchoredPosition = _restPosition + Vector2.down * (slideDistance * (1f - Mathf.SmoothStep(0f, 1f, visibility)));
        }
    }
}
