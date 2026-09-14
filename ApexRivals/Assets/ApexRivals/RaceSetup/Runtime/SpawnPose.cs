using UnityEngine;

namespace ApexRivals.RaceSetup.Runtime
{
    public readonly struct SpawnPose
    {
        public SpawnPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }
}
