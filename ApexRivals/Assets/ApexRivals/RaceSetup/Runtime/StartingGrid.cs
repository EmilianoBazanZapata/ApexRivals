using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    public sealed class StartingGrid : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();

        public int SpawnPointCount => spawnPoints.Length;

        public void Configure(Transform[] orderedSpawnPoints)
        {
            spawnPoints = orderedSpawnPoints ?? Array.Empty<Transform>();
        }

        public bool TryGetSpawnPose(int gridIndex, out SpawnPose spawnPose)
        {
            spawnPose = default;
            if (gridIndex < 0 || gridIndex >= spawnPoints.Length || spawnPoints[gridIndex] == null)
            {
                return false;
            }

            var spawnPoint = spawnPoints[gridIndex];
            spawnPose = new SpawnPose(spawnPoint.position, spawnPoint.rotation);
            return true;
        }

        public RaceRosterValidationResult Validate()
        {
            var assigned = new HashSet<Transform>();
            for (var index = 0; index < spawnPoints.Length; index++)
            {
                var spawnPoint = spawnPoints[index];
                if (spawnPoint == null)
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.InsufficientSpawnPoints, $"Starting grid index '{index}' is missing a spawn point.");
                }

                if (!assigned.Add(spawnPoint))
                {
                    return new RaceRosterValidationResult(RaceRosterValidationStatus.DuplicateGridIndex, $"Starting grid index '{index}' uses a duplicate spawn point.");
                }
            }

            return new RaceRosterValidationResult(RaceRosterValidationStatus.Succeeded, string.Empty);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (var index = 0; index < spawnPoints.Length; index++)
            {
                var spawnPoint = spawnPoints[index];
                if (spawnPoint == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
                Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + spawnPoint.forward * 1.5f);
            }
        }
    }
}
