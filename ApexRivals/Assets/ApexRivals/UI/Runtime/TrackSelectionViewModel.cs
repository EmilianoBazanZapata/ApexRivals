using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public readonly struct TrackSelectionItemViewModel
    {
        public TrackSelectionItemViewModel(
            string trackId,
            string displayName,
            string location,
            string lengthText,
            int laps,
            string environment,
            string surface,
            Sprite preview,
            IReadOnlyList<Vector2> minimapPoints)
        {
            TrackId = trackId;
            DisplayName = displayName;
            Location = location;
            LengthText = lengthText;
            Laps = laps;
            Environment = environment;
            Surface = surface;
            Preview = preview;
            MinimapPoints = minimapPoints ?? Array.Empty<Vector2>();
        }

        public string TrackId { get; }
        public string DisplayName { get; }
        public string Location { get; }
        public string LengthText { get; }
        public int Laps { get; }
        public string Environment { get; }
        public string Surface { get; }
        public Sprite Preview { get; }
        public IReadOnlyList<Vector2> MinimapPoints { get; }
    }

    public readonly struct TrackSelectionViewModel
    {
        public TrackSelectionViewModel(
            IReadOnlyList<TrackSelectionItemViewModel> tracks,
            int selectedIndex,
            bool operationActive,
            PresentationStatus status,
            string messageKey)
        {
            Tracks = tracks ?? Array.Empty<TrackSelectionItemViewModel>();
            SelectedIndex = selectedIndex;
            OperationActive = operationActive;
            Status = status;
            MessageKey = messageKey ?? string.Empty;
        }

        public IReadOnlyList<TrackSelectionItemViewModel> Tracks { get; }
        public int SelectedIndex { get; }
        public bool OperationActive { get; }
        public PresentationStatus Status { get; }
        public string MessageKey { get; }

        public bool HasSelection => SelectedIndex >= 0 && SelectedIndex < Tracks.Count;
        public bool CanStartRace => HasSelection && !OperationActive;
        public TrackSelectionItemViewModel Selected => HasSelection ? Tracks[SelectedIndex] : default;
    }
}
