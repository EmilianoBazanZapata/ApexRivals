using ApexRivals.Race.Runtime;
using UnityEngine;

namespace ApexRivals.Race.Configuration
{
    [CreateAssetMenu(fileName = "RaceDefinition", menuName = "Apex Rivals/Race/Race Definition")]
    public sealed class RaceDefinition : ScriptableObject
    {
        [SerializeField]
        private string displayName = "Development Race";

        [SerializeField, Min(RaceRules.MinimumLaps)]
        private int totalLaps = 3;

        [SerializeField, Min(RaceRules.MinimumCountdownDuration)]
        private float countdownDuration = 3f;

        public string DisplayName => displayName;
        public int TotalLaps => totalLaps;
        public float CountdownDuration => countdownDuration;

        private void OnValidate()
        {
            totalLaps = RaceRules.ClampTotalLaps(totalLaps);
            countdownDuration = RaceRules.ClampCountdownDuration(countdownDuration);

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "Development Race";
            }
        }
    }
}
