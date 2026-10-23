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
            IReadOnlyList<Vector2> minimapPoints,
            bool isLocked = false,
            string lockText = "",
            string statusText = "",
            bool isFinale = false)
        {
            IsLocked = isLocked;
            LockText = lockText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            IsFinale = isFinale;
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

        /// <summary>Visible but not raceable yet; <see cref="LockText"/> says what opens it.</summary>
        public bool IsLocked { get; }
        public string LockText { get; }

        /// <summary>"WON", "COMPLETED" or "CHAMPION"; empty when not raced yet.</summary>
        public string StatusText { get; }

        /// <summary>The career final entry (held on <see cref="TrackId"/>), not a normal race there.</summary>
        public bool IsFinale { get; }
    }

    public readonly struct TrackSelectionViewModel
    {
        public TrackSelectionViewModel(
            IReadOnlyList<TrackSelectionItemViewModel> tracks,
            int selectedIndex,
            bool operationActive,
            PresentationStatus status,
            string messageKey,
            string feedback = "",
            string objectiveText = "")
        {
            Feedback = feedback ?? string.Empty;
            ObjectiveText = objectiveText ?? string.Empty;
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

        /// <summary>Reason a race could not start (e.g. the circuit's requirement); empty otherwise.</summary>
        public string Feedback { get; }

        /// <summary>Career completion and next objective; empty without a career.</summary>
        public string ObjectiveText { get; }

        public bool HasSelection => SelectedIndex >= 0 && SelectedIndex < Tracks.Count;
        public bool CanStartRace => HasSelection && !OperationActive && !Tracks[SelectedIndex].IsLocked;
        public TrackSelectionItemViewModel Selected => HasSelection ? Tracks[SelectedIndex] : default;
    }
}
