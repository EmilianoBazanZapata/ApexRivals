using System.Collections.Generic;
using ApexRivals.Race.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// Projects every other racer onto the driver's ideal path so gaps and lateral positions are measured
    /// along the track, not in straight lines that cut across corners.
    /// </summary>
    public sealed class AiTrafficPerception
    {
        private const int ProjectionWindow = 12;

        /// <summary>Beyond this distance from the local projection a racer is assumed to have been reset elsewhere.</summary>
        private const float RelocateDistance = 15f;

        private readonly Dictionary<RaceParticipant, Tracked> _tracked = new Dictionary<RaceParticipant, Tracked>();
        private readonly List<AiTrafficCar> _cars = new List<AiTrafficCar>();
        private readonly List<RaceParticipant> _stale = new List<RaceParticipant>();

        private sealed class Tracked
        {
            public int Anchor = -1;
            public Rigidbody Body;
            public bool Seen;
        }

        public IReadOnlyList<AiTrafficCar> Cars => _cars;

        public void Clear()
        {
            _tracked.Clear();
            _cars.Clear();
        }

        public void Refresh(IdealRacingPath path, IdealRacingPath.Projection self, RaceParticipant selfParticipant, IReadOnlyList<RaceParticipant> participants)
        {
            _cars.Clear();
            if (path == null || participants == null)
            {
                return;
            }

            foreach (var entry in _tracked.Values)
            {
                entry.Seen = false;
            }

            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                if (participant == null || participant == selfParticipant || !participant.isActiveAndEnabled)
                {
                    continue;
                }

                if (!_tracked.TryGetValue(participant, out var tracked))
                {
                    tracked = new Tracked { Body = participant.GetComponentInParent<Rigidbody>() };
                    _tracked.Add(participant, tracked);
                }

                tracked.Seen = true;
                var position = participant.ProgressTransform.position;
                if (!path.TryProjectTracked(position, tracked.Anchor, ProjectionWindow, RelocateDistance, out var projection))
                {
                    continue;
                }

                tracked.Anchor = projection.SegmentIndex;
                var velocity = tracked.Body != null ? tracked.Body.linearVelocity : Vector3.zero;
                var speedAlongPath = Vector3.Dot(velocity, projection.Tangent);
                var gap = AiRacecraftRules.WrapLapGap(projection.DistanceAlongLap, self.DistanceAlongLap, path.LapLength);
                _cars.Add(new AiTrafficCar(gap, projection.CrossTrackError, speedAlongPath));
            }

            _stale.Clear();
            foreach (var pair in _tracked)
            {
                if (!pair.Value.Seen)
                {
                    _stale.Add(pair.Key);
                }
            }

            for (var index = 0; index < _stale.Count; index++)
            {
                _tracked.Remove(_stale[index]);
            }
        }
    }
}
