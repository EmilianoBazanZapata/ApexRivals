using ApexRivals.AI.Configuration;
using ApexRivals.Race.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// Owns one driver's ideal path and where the car sits on it: the local projection, the sample it is
    /// progressing from, and where each checkpoint falls along the lap.
    /// </summary>
    public sealed class AiPathTracker
    {
        private IdealRacingPath.Projection _projection;
        private float[] _checkpointDistances;

        public IdealRacingPath Path { get; private set; }
        public IdealRacingPath.Projection Projection => _projection;
        public bool HasProjection { get; private set; }
        public int CurrentSampleIndex { get; private set; }

        public float DistanceFromLine => HasProjection ? _projection.Distance : 0f;
        public float CrossTrackError => HasProjection ? _projection.CrossTrackError : 0f;
        public float DistanceAlongLap => HasProjection ? _projection.DistanceAlongLap : 0f;

        public void Clear()
        {
            Path = null;
            HasProjection = false;
            CurrentSampleIndex = 0;
            _checkpointDistances = null;
        }

        /// <summary>Builds the path with the driver's speed profile and anchors it at <paramref name="position"/>.</summary>
        public bool TryBuild(RacingLine line, float sampleSpacing, AiDriverConfiguration configuration, Vector3 position, RaceCoordinator coordinator)
        {
            Clear();
            if (line == null || !line.IsValid || configuration == null)
            {
                return false;
            }

            var tuning = configuration.CreateTuning();
            var grip = configuration.CorneringGrip > 0f ? configuration.CorneringGrip : float.PositiveInfinity;
            if (!IdealRacingPath.TryBuild(line, sampleSpacing, tuning.TargetSpeed, tuning.MinimumCornerSpeed, configuration.CurvatureSpeedScale, grip, out var path))
            {
                return false;
            }

            Path = path;
            CurrentSampleIndex = path.FindNearestSegment(position);
            MapCheckpoints(coordinator);
            return true;
        }

        /// <summary>
        /// Projects within a local window around the current sample so the car never jumps to a parallel
        /// stretch of track; the anchor only moves forward within the window.
        /// </summary>
        public bool TryProject(Vector3 position, int backwardWindow, int forwardWindow)
        {
            if (Path == null || !Path.TryProjectLocal(position, CurrentSampleIndex, backwardWindow, forwardWindow, out var projection))
            {
                HasProjection = false;
                return false;
            }

            if (IdealRacingDriverRules.ForwardIndexDistance(CurrentSampleIndex, projection.SegmentIndex, Path.SampleCount) <= forwardWindow)
            {
                CurrentSampleIndex = projection.SegmentIndex;
            }

            _projection = projection;
            HasProjection = true;
            return true;
        }

        /// <summary>Re-anchors after the car was moved, e.g. by a reset.</summary>
        public void Reanchor(int sampleIndex, Vector3 position)
        {
            if (Path == null)
            {
                return;
            }

            CurrentSampleIndex = sampleIndex >= 0 ? sampleIndex : Path.FindNearestSegment(position);
        }

        /// <summary>Distance along the lap to the next checkpoint the racer must cross; -1 when unknown.</summary>
        public float GetGapToNextCheckpoint(RaceCoordinator coordinator, RaceParticipant participant)
        {
            if (coordinator == null || participant == null || _checkpointDistances == null || !HasProjection
                || !coordinator.TryGetProgress(participant, out var progress) || progress.HasFinished)
            {
                return -1f;
            }

            var index = progress.NextExpectedCheckpointIndex;
            if (index < 0 || index >= _checkpointDistances.Length || _checkpointDistances[index] < 0f)
            {
                return -1f;
            }

            return Mathf.Repeat(_checkpointDistances[index] - _projection.DistanceAlongLap, Path.LapLength);
        }

        private void MapCheckpoints(RaceCoordinator coordinator)
        {
            if (coordinator == null || coordinator.OrderedCheckpoints == null)
            {
                return;
            }

            var checkpoints = coordinator.OrderedCheckpoints;
            _checkpointDistances = new float[checkpoints.Count];
            for (var index = 0; index < checkpoints.Count; index++)
            {
                var checkpoint = checkpoints[index];
                _checkpointDistances[index] = -1f;
                if (checkpoint == null)
                {
                    continue;
                }

                var anchor = Path.FindNearestSegment(checkpoint.Position);
                if (Path.TryProjectLocal(checkpoint.Position, anchor, 2, 2, out var projection))
                {
                    _checkpointDistances[index] = projection.DistanceAlongLap;
                }
            }
        }
    }
}
