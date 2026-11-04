using System;
using System.Collections.Generic;
using ApexRivals.AI.Runtime;
using ApexRivals.Race.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Minimap data from the race scene: the AI racing line as the outline and the coordinator's racers.</summary>
    public sealed class RaceCoordinatorMinimapSource : IRaceMinimapSource
    {
        private readonly RaceCoordinator _coordinator;
        private readonly Vector2[] _trackLine;

        public RaceCoordinatorMinimapSource(RaceCoordinator coordinator, RacingLine racingLine)
        {
            _coordinator = coordinator != null ? coordinator : throw new ArgumentNullException(nameof(coordinator));
            if (racingLine == null)
            {
                throw new ArgumentNullException(nameof(racingLine));
            }

            _trackLine = new Vector2[racingLine.WaypointCount];
            for (var index = 0; index < _trackLine.Length; index++)
            {
                var waypoint = racingLine.GetWaypointPosition(index);
                _trackLine[index] = new Vector2(waypoint.x, waypoint.z);
            }
        }

        public IReadOnlyList<Vector2> TrackLine => _trackLine;

        public int CarCount => _coordinator != null ? _coordinator.Participants.Count : 0;

        public bool TryGetCar(int index, out Vector2 position, out bool isPlayer)
        {
            position = default;
            isPlayer = false;
            if (_coordinator == null || index < 0 || index >= _coordinator.Participants.Count)
            {
                return false;
            }

            var participant = _coordinator.Participants[index];
            if (participant == null)
            {
                return false;
            }

            var world = participant.ProgressTransform.position;
            position = new Vector2(world.x, world.z);
            isPlayer = participant == _coordinator.PlayerParticipant;
            return true;
        }
    }
}
