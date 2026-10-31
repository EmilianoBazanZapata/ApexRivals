using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityObject = UnityEngine.Object;
using ApexRivals.Career.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Tracks.Runtime;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Race Select screen opened from the main menu: browses the track catalog, loads the chosen track's
    /// scene and returns to the main menu on Back. With a career, locked circuits stay visible
    /// with their requirement and the career final is listed as an extra event after the circuits.
    /// </summary>
    public sealed class TrackSelectionPresenter : IDisposable
    {
        private ITrackSelectionView _view;
        private readonly TrackCatalog _catalog;
        private readonly SceneFlowService _sceneFlow;
        private readonly IPresentationScreenNavigator _screenNavigator;
        private readonly CareerService _career;
        private List<TrackSelectionItemViewModel> _items;
        private int _selectedIndex;
        private bool _operationActive;
        private bool _disposed;
        private PresentationStatus _status;
        private string _messageKey = string.Empty;
        private string _feedback = string.Empty;

        public TrackSelectionPresenter(ITrackSelectionView view, TrackCatalog catalog, SceneFlowService sceneFlow, IPresentationScreenNavigator screenNavigator, CareerService career = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            _screenNavigator = screenNavigator ?? throw new ArgumentNullException(nameof(screenNavigator));
            _career = career;
            _items = CreateItems();

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

            // Unlocks can change while the garage is open (purchases), so rebuild the cards every time.
            _items = CreateItems();
            var careerPercent = -1;
            var careerNext = string.Empty;
            if (_career != null)
            {
                var summary = _career.GetSummary();
                careerPercent = Math.Max(0, Math.Min(100, summary.CompletionPercent));
                careerNext = summary.IsComplete ? "CAREER COMPLETE" : "NEXT: " + summary.Objective.Title.ToUpperInvariant();
            }

            Current = new TrackSelectionViewModel(_items, _selectedIndex, _operationActive, _status, _messageKey, _feedback, CreateObjectiveText(), careerPercent, careerNext);
            _view.Render(Current);
        }

        public void Open()
        {
            if (!CanRender || _operationActive) return;

            _status = PresentationStatus.None;
            _messageKey = string.Empty;
            _feedback = string.Empty;
            _screenNavigator.Show(PresentationScreenState.TrackSelection);
            Present();
        }

        /// <summary>Back (button or Cancel) leaves Track Select for the main menu, where the screen is opened from.</summary>
        public async Task Back()
        {
            if (!CanRender || _operationActive) return;

            _operationActive = true;
            Present();
            var result = await _sceneFlow.OpenMainMenu();
            if (!CanRender) return;
            _operationActive = false;
            if (!result.Succeeded)
            {
                Fail("ui.trackSelect.backFailed");
                return;
            }

            Present();
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
            _feedback = string.Empty;
            Present();
        }

        public void SelectPreviousTrack()
        {
            SelectTrack(WrapIndex(_selectedIndex - 1));
        }

        public void SelectNextTrack()
        {
            SelectTrack(WrapIndex(_selectedIndex + 1));
        }

        public async Task StartRace()
        {
            if (!CanRender || _operationActive) return;
            if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
            {
                Fail("ui.trackSelect.noTrack");
                return;
            }

            var item = _items[_selectedIndex];
            var sceneName = _catalog.Tracks[_catalog.IndexOf(item.TrackId)].SceneName;
            if (_career != null)
            {
                // The career decides; the screen only reports why a race cannot start.
                var request = _career.PrepareRace(item.TrackId, item.IsFinale);
                if (!request.Succeeded)
                {
                    Fail("ui.trackSelect.locked", request.Message);
                    return;
                }

                sceneName = request.SceneName;
            }

            if (!_sceneFlow.SelectRaceScene(sceneName))
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
                Fail("ui.trackSelect.startRaceFailed", result.Status == SceneTransitionStatus.RaceStartRejected ? result.Message : string.Empty);
                return;
            }

            Present();
        }

        private void Fail(string messageKey, string feedback = "")
        {
            _status = PresentationStatus.Invalid;
            _messageKey = messageKey;
            _feedback = feedback ?? string.Empty;
            Present();
        }

        private int WrapIndex(int index)
        {
            return _items.Count == 0 ? -1 : (index % _items.Count + _items.Count) % _items.Count;
        }

        private string CreateObjectiveText()
        {
            if (_career == null)
            {
                return string.Empty;
            }

            var summary = _career.GetSummary();
            return summary.IsComplete
                ? string.Format(CultureInfo.InvariantCulture, "CAREER COMPLETE - {0}%", summary.CompletionPercent)
                : string.Format(CultureInfo.InvariantCulture, "CAREER {0}% - NEXT: {1}", summary.CompletionPercent, summary.Objective.Title.ToUpperInvariant());
        }

        private List<TrackSelectionItemViewModel> CreateItems()
        {
            var items = new List<TrackSelectionItemViewModel>(_catalog.Count + 1);
            for (var index = 0; index < _catalog.Count; index++)
            {
                var track = _catalog.Tracks[index];
                var lockText = string.Empty;
                var statusText = string.Empty;
                var locked = false;
                if (_career != null)
                {
                    var status = _career.GetTrackStatus(track.TrackId);
                    locked = !status.Unlocked;
                    lockText = locked ? "REQUIRES: " + status.RequirementTitle.ToUpperInvariant() : string.Empty;
                    statusText = status.Won ? "WON" : status.Completed ? "COMPLETED" : string.Empty;
                }

                items.Add(new TrackSelectionItemViewModel(
                    track.TrackId,
                    track.DisplayName,
                    track.Location,
                    UguiViewText.FormatTrackLength(track.LengthMeters),
                    track.Laps,
                    track.Environment,
                    track.Surface,
                    track.Preview,
                    track.MinimapPoints,
                    locked,
                    lockText,
                    statusText,
                    false));
            }

            AddFinale(items);
            return items;
        }

        /// <summary>The final is its own entry so it can never be mistaken for a normal race on its circuit.</summary>
        private void AddFinale(List<TrackSelectionItemViewModel> items)
        {
            var finale = _career?.Configuration.Finale;
            var trackIndex = finale != null ? _catalog.IndexOf(finale.TrackId) : -1;
            if (trackIndex < 0)
            {
                return;
            }

            var track = _catalog.Tracks[trackIndex];
            var status = _career.GetFinaleStatus();
            var lockText = status.Qualified
                ? string.Empty
                : string.Format(CultureInfo.InvariantCulture, "REQUIRES: {0} ({1}/{2})", status.NextMissingTitle.ToUpperInvariant(), status.MetRequirements, status.TotalRequirements);
            items.Add(new TrackSelectionItemViewModel(
                track.TrackId,
                finale.Title,
                "FINAL EVENT - " + track.DisplayName,
                UguiViewText.FormatTrackLength(track.LengthMeters),
                track.Laps,
                track.Environment,
                track.Surface,
                track.Preview,
                track.MinimapPoints,
                !status.Qualified,
                lockText,
                status.State == CareerFinaleState.Won ? "CHAMPION" : string.Empty,
                true,
                finale.Goal));
        }
    }
}
