using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    public readonly struct RacerPositionSnapshot
    {
        public RacerPositionSnapshot(RacerProgress progress, Vector3 position)
        {
            Progress = progress;
            Position = position;
        }

        public RacerProgress Progress { get; }
        public Vector3 Position { get; }
    }
}
