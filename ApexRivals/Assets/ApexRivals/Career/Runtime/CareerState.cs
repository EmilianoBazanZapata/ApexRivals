using System;
using System.Collections.Generic;

namespace ApexRivals.Career.Runtime
{
    /// <summary>
    /// Persisted career facts: cars owned, per-track completions and wins, achieved milestones (which are also
    /// the ledger of one-time rewards already paid) and whether the final was won. Every set only grows during
    /// play. IDs the current configuration does not know are kept so a content change never erases progress.
    /// </summary>
    public sealed class CareerState
    {
        private readonly IdSet _ownedVehicles = new IdSet();
        private readonly IdSet _completedTracks = new IdSet();
        private readonly IdSet _wonTracks = new IdSet();
        private readonly IdSet _achievedMilestones = new IdSet();

        public event Action Changed;

        public IReadOnlyList<string> OwnedVehicleIds => _ownedVehicles.Items;
        public IReadOnlyList<string> CompletedTrackIds => _completedTracks.Items;
        public IReadOnlyList<string> WonTrackIds => _wonTracks.Items;
        public IReadOnlyList<string> AchievedMilestoneIds => _achievedMilestones.Items;
        public bool FinaleWon { get; private set; }

        public bool OwnsVehicle(string vehicleId) => _ownedVehicles.Contains(vehicleId);
        public bool HasCompletedTrack(string trackId) => _completedTracks.Contains(trackId);
        public bool HasWonTrack(string trackId) => _wonTracks.Contains(trackId);
        public bool HasAchieved(string milestoneId) => _achievedMilestones.Contains(milestoneId);

        public bool GrantVehicle(string vehicleId) => Notify(_ownedVehicles.Add(vehicleId));
        public bool MarkTrackCompleted(string trackId) => Notify(_completedTracks.Add(trackId));
        public bool MarkTrackWon(string trackId) => Notify(_wonTracks.Add(trackId));
        public bool MarkMilestoneAchieved(string milestoneId) => Notify(_achievedMilestones.Add(milestoneId));

        public bool MarkFinaleWon()
        {
            if (FinaleWon)
            {
                return false;
            }

            FinaleWon = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Replaces every fact. Blank and repeated IDs are dropped.</summary>
        public void Restore(
            IEnumerable<string> ownedVehicleIds,
            IEnumerable<string> completedTrackIds,
            IEnumerable<string> wonTrackIds,
            IEnumerable<string> achievedMilestoneIds,
            bool finaleWon)
        {
            _ownedVehicles.Reset(ownedVehicleIds);
            _completedTracks.Reset(completedTrackIds);
            _wonTracks.Reset(wonTrackIds);
            _achievedMilestones.Reset(achievedMilestoneIds);
            FinaleWon = finaleWon;
            Changed?.Invoke();
        }

        public void Clear()
        {
            Restore(null, null, null, null, false);
        }

        public void CopyFrom(CareerState other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Restore(other.OwnedVehicleIds, other.CompletedTrackIds, other.WonTrackIds, other.AchievedMilestoneIds, other.FinaleWon);
        }

        public CareerState Clone()
        {
            var clone = new CareerState();
            clone.CopyFrom(this);
            return clone;
        }

        private bool Notify(bool changed)
        {
            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        /// <summary>Insertion-ordered set of trimmed, non-empty IDs.</summary>
        private sealed class IdSet
        {
            private readonly List<string> _items = new List<string>();
            private readonly HashSet<string> _lookup = new HashSet<string>(StringComparer.Ordinal);

            public IReadOnlyList<string> Items => _items;

            public bool Contains(string id) => !string.IsNullOrWhiteSpace(id) && _lookup.Contains(id.Trim());

            public bool Add(string id)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                var normalized = id.Trim();
                if (!_lookup.Add(normalized))
                {
                    return false;
                }

                _items.Add(normalized);
                return true;
            }

            public void Reset(IEnumerable<string> ids)
            {
                _items.Clear();
                _lookup.Clear();
                if (ids == null)
                {
                    return;
                }

                foreach (var id in ids)
                {
                    Add(id);
                }
            }
        }
    }
}
