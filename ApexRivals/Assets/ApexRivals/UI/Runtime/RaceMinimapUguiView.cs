using System.Collections.Generic;
using ApexRivals.Tracks.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Bottom-left race minimap: the track outline plus one marker per car, refreshed every frame. The player's
    /// marker uses its own (larger, accented) template and is always drawn above the rivals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaceMinimapUguiView : MonoBehaviour
    {
        [SerializeField] private TrackMinimapGraphic track;
        [SerializeField, Tooltip("Inactive template cloned for each rival.")] private RectTransform rivalMarkerTemplate;
        [SerializeField, Tooltip("Inactive template used for the player.")] private RectTransform playerMarkerTemplate;

        private readonly List<RectTransform> _rivalMarkers = new List<RectTransform>();
        private RectTransform _playerMarker;
        private IRaceMinimapSource _source;
        private TrackMinimapFrame _frame;

        public void Bind(IRaceMinimapSource source)
        {
            _source = source;
            if (track == null)
            {
                return;
            }

            if (source == null)
            {
                track.SetPoints(null);
                HideMarkers(0, false);
                return;
            }

            _frame = TrackMinimap.Fit(source.TrackLine);
            track.SetPoints(TrackMinimap.Normalize(source.TrackLine));
            RefreshMarkers();
        }

        private void LateUpdate()
        {
            if (_source != null)
            {
                RefreshMarkers();
            }
        }

        private void RefreshMarkers()
        {
            var rivals = 0;
            var playerShown = false;
            for (var index = 0; index < _source.CarCount; index++)
            {
                if (!_source.TryGetCar(index, out var position, out var isPlayer)
                    || !track.TryMapNormalized(_frame.ToNormalized(position), out var local))
                {
                    continue;
                }

                var marker = isPlayer ? PlayerMarker() : RivalMarker(rivals++);
                if (marker == null)
                {
                    continue;
                }

                marker.localPosition = new Vector3(local.x, local.y, 0f);
                if (!marker.gameObject.activeSelf)
                {
                    marker.gameObject.SetActive(true);
                }

                playerShown |= isPlayer;
            }

            HideMarkers(rivals, playerShown);
            if (playerShown)
            {
                _playerMarker.SetAsLastSibling();
            }
        }

        private RectTransform PlayerMarker()
        {
            if (_playerMarker == null && playerMarkerTemplate != null)
            {
                _playerMarker = Instantiate(playerMarkerTemplate, track.transform);
                _playerMarker.name = "PlayerMarker";
            }

            return _playerMarker;
        }

        private RectTransform RivalMarker(int index)
        {
            while (_rivalMarkers.Count <= index && rivalMarkerTemplate != null)
            {
                var marker = Instantiate(rivalMarkerTemplate, track.transform);
                marker.name = $"RivalMarker_{_rivalMarkers.Count}";
                _rivalMarkers.Add(marker);
            }

            return index < _rivalMarkers.Count ? _rivalMarkers[index] : null;
        }

        private void HideMarkers(int visibleRivals, bool playerVisible)
        {
            for (var index = visibleRivals; index < _rivalMarkers.Count; index++)
            {
                if (_rivalMarkers[index].gameObject.activeSelf)
                {
                    _rivalMarkers[index].gameObject.SetActive(false);
                }
            }

            if (_playerMarker != null && !playerVisible && _playerMarker.gameObject.activeSelf)
            {
                _playerMarker.gameObject.SetActive(false);
            }
        }
    }
}
