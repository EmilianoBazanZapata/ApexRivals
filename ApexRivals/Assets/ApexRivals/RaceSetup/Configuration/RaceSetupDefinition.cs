using System;
using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.Race.Configuration;
using ApexRivals.RaceSetup.Runtime;
using UnityEngine;

namespace ApexRivals.RaceSetup.Configuration
{
    [CreateAssetMenu(fileName = "RaceSetupDefinition", menuName = "Apex Rivals/Race Setup/Race Setup Definition")]
    public sealed class RaceSetupDefinition : ScriptableObject
    {
        [SerializeField] private RaceDefinition raceDefinition;
        [SerializeField] private string playerParticipantId = "Player";
        [SerializeField] private string playerVehicleId = "starter";
        [SerializeField, Min(0)] private int playerStartingGridIndex;
        [SerializeField] private RaceAiOpponentDefinition[] aiOpponents = Array.Empty<RaceAiOpponentDefinition>();

        public RaceDefinition RaceDefinition => raceDefinition;
        public IReadOnlyList<RaceAiOpponentDefinition> AiOpponents => aiOpponents;

        public RaceRoster CreateRoster(string selectedPlayerVehicleId)
        {
            var entries = new List<RaceRosterEntry>
            {
                new RaceRosterEntry(
                    playerParticipantId,
                    RaceEntryType.Player,
                    string.IsNullOrWhiteSpace(selectedPlayerVehicleId) ? string.Empty : selectedPlayerVehicleId.Trim(),
                    playerStartingGridIndex,
                    string.Empty)
            };

            for (var index = 0; index < aiOpponents.Length; index++)
            {
                var opponent = aiOpponents[index];
                entries.Add(new RaceRosterEntry(
                    opponent.ParticipantId,
                    RaceEntryType.AI,
                    opponent.VehicleId,
                    opponent.StartingGridIndex,
                    opponent.AiConfigurationId));
            }

            return new RaceRoster(entries);
        }

        public IReadOnlyDictionary<string, AiDriverConfiguration> CreateAiConfigurationMap()
        {
            var configurations = new Dictionary<string, AiDriverConfiguration>(StringComparer.Ordinal);
            for (var index = 0; index < aiOpponents.Length; index++)
            {
                var opponent = aiOpponents[index];
                if (!string.IsNullOrWhiteSpace(opponent.AiConfigurationId) && !configurations.ContainsKey(opponent.AiConfigurationId))
                {
                    configurations.Add(opponent.AiConfigurationId, opponent.AiConfiguration);
                }
            }

            return configurations;
        }
    }
}
