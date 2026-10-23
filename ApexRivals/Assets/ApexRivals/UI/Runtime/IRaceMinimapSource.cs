using System.Collections.Generic;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    /// <summary>What the race minimap draws: the track outline and every car, all in world x/z metres.</summary>
    public interface IRaceMinimapSource
    {
        /// <summary>Closed racing line, start/finish line first.</summary>
        IReadOnlyList<Vector2> TrackLine { get; }

        int CarCount { get; }

        bool TryGetCar(int index, out Vector2 position, out bool isPlayer);
    }
}
