using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    public static class RaceRules
    {
        public const int MinimumLaps = 1;
        public const int MinimumCheckpointCount = 1;
        public const float MinimumCountdownDuration = 0f;

        public static int ClampTotalLaps(int totalLaps)
        {
            return Mathf.Max(MinimumLaps, totalLaps);
        }

        public static float ClampCountdownDuration(float countdownDuration)
        {
            return Mathf.Max(MinimumCountdownDuration, countdownDuration);
        }

        public static bool IsCheckpointSequenceValid(IReadOnlyList<int> checkpointIndices)
        {
            if (checkpointIndices == null || checkpointIndices.Count < MinimumCheckpointCount)
            {
                return false;
            }

            for (var index = 0; index < checkpointIndices.Count; index++)
            {
                if (checkpointIndices[index] != index)
                {
                    return false;
                }
            }

            return true;
        }

        public static int CompareRacePosition(RacerPositionSnapshot left, RacerPositionSnapshot right, IReadOnlyList<Vector3> checkpointPositions)
        {
            var leftProgress = left.Progress;
            var rightProgress = right.Progress;

            if (leftProgress.HasFinished != rightProgress.HasFinished)
            {
                return leftProgress.HasFinished ? -1 : 1;
            }

            if (leftProgress.HasFinished && rightProgress.HasFinished)
            {
                return leftProgress.FinalPosition.CompareTo(rightProgress.FinalPosition);
            }

            var completedLapComparison = rightProgress.CompletedLaps.CompareTo(leftProgress.CompletedLaps);
            if (completedLapComparison != 0)
            {
                return completedLapComparison;
            }

            var checkpointComparison = rightProgress.TotalPassedCheckpoints.CompareTo(leftProgress.TotalPassedCheckpoints);
            if (checkpointComparison != 0)
            {
                return checkpointComparison;
            }

            var leftDistance = DistanceToNextCheckpoint(left, checkpointPositions);
            var rightDistance = DistanceToNextCheckpoint(right, checkpointPositions);
            var distanceComparison = leftDistance.CompareTo(rightDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return string.CompareOrdinal(leftProgress.RacerId, rightProgress.RacerId);
        }

        private static float DistanceToNextCheckpoint(RacerPositionSnapshot snapshot, IReadOnlyList<Vector3> checkpointPositions)
        {
            if (checkpointPositions == null || checkpointPositions.Count == 0)
            {
                return float.PositiveInfinity;
            }

            var nextIndex = Mathf.Clamp(snapshot.Progress.NextExpectedCheckpointIndex, 0, checkpointPositions.Count - 1);
            return Vector3.Distance(snapshot.Position, checkpointPositions[nextIndex]);
        }
    }
}
