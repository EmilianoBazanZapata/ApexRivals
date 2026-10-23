using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityObject = UnityEngine.Object;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Tracks.Runtime;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Race Select screen shown between the garage and the race: browses the track catalog, loads the
    /// chosen track's scene and returns to the garage on Back.
    /// </summary>
    public sealed class TrackSelectionPresenter : IDisposable
    {
        private ITrackSelectionView _view;
        private readonly TrackCatalog _catalog;
        private readonly SceneFlowService _sceneFlow;
        private readonly IPresentationScreenNavigator _screenNavigator;
        private readonly List<TrackSelectionItemViewModel> _items;
        private int _selectedIndex;
        private bool _operationActive;
        private bool _disposed;
        private PresentationStatus _status;
        private string _messageKey = string.Empty;

        public TrackSelectionPresenter(ITrackSelectionView view, TrackCatalog catalog, SceneFlowService sceneFlow, IPresentationScreenNavigator screenNavigator)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            _screenNavigator = screenNavigator ?? throw new ArgumentNullException(nameof(screenNavigator));
            _items = CreateItems(catalog);

            // Coming back from a race re-opens on the track that was just raced.
            var raced = catalog.IndexOfScene(sceneFlow.SelectedRaceSceneName);
            _selectedIndex = raced >= 0 ? raced : catalog.DefaultTrackIndex;
        }

        public TrackSelectionViewModel Current { get; private set; }

        private bool CanRender => !_disposed && _view != null
            && (_view is not UnityObject unityView || unityView != null);

        public void Dispose()
        {
            _disposed = true;
            _view = null;
        }

        public void Present()
        {
            if (!CanRender) return;

            Current = new TrackSelectionViewModel(_items, _selectedIndex, _operationActive, _status, _messageKey);
            _view.Render(Current);
        }

        public void Open()
        {
            if (!CanRender || _operationActive) return;

            _status = PresentationStatus.None;
            _messageKey = string.Empty;
            _screenNavigator.Show(PresentationScreenState.TrackSelection);
            Present();
        }

        public void Back()
        {
            if (!CanRender || _operationActive) return;

            _screenNavigator.Show(PresentationScreenState.Garage);
        }

        public void SelectTrack(int index)
        {
            if (!CanRender || _operationActive || index < 0 || index >= _items.Count || index == _selectedIndex)
            {
                return;
            }

            _selectedIndex = index;
            _status = PresentationStatus.None;
            _messageKey = string.Empty;
            Present();
        }

        public void SelectPreviousTrack()
        {
            SelectTrack(_catalog.WrapIndex(_selectedIndex - 1));
        }

        public void SelectNextTrack()
        {
            SelectTrack(_catalog.WrapIndex(_selectedIndex + 1));
        }

        public async Task StartRace()
        {
            if (!CanRender || _operationActive) return;
            if (_selectedIndex < 0 || _selectedIndex >= _catalog.Count)
            {
                Fail("ui.trackSelect.noTrack");
                return;
            }

            if (!_sceneFlow.SelectRaceScene(_catalog.Tracks[_selectedIndex].SceneName))
            {
                Fail("ui.trackSelect.startRaceFailed");
                return;
            }

            _operationActive = true;
            Present();
            var result = await _sceneFlow.StartRace();
            if (!CanRender) return;
            _operationActive = false;
            if (!result.Succeeded)
            {
                Fail("ui.trackSelect.startRaceFailed");
                return;
            }

            Present();
        }

        private void Fail(string messageKey)
        {
            _status = PresentationStatus.Invalid;
            _messageKey = messageKey;
            Present();
        }

        private static List<TrackSelectionItemViewModel> CreateItems(TrackCatalog catalog)
        {
            var items = new List<TrackSelectionItemViewModel>(catalog.Count);
            for (var index = 0; index < catalog.Count; index++)
            {
                var track = catalog.Tracks[index];
                items.Add(new TrackSelectionItemViewModel(
                    track.TrackId,
                    track.DisplayName,
                    track.Location,
                    UguiViewText.FormatTrackLength(track.LengthMeters),
                    track.Laps,
                    track.Environment,
                    track.Surface,
                    track.Preview,
                    track.MinimapPoints));
            }

            return items;
        }
    }
}
