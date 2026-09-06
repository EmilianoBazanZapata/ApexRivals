using System;
using UnityEngine;

namespace ApexRivals.SaveSystem.Runtime
{
    public sealed class PlayerProfileJsonSerializer
    {
        public SaveSerializationResult Serialize(PlayerProfileSaveData saveData)
        {
            if (saveData == null)
            {
                return new SaveSerializationResult(false, string.Empty, "Save data is missing.");
            }

            try
            {
                var json = JsonUtility.ToJson(saveData, true);
                return string.IsNullOrWhiteSpace(json)
                    ? new SaveSerializationResult(false, string.Empty, "Serialized save data is empty.")
                    : new SaveSerializationResult(true, json, string.Empty);
            }
            catch (Exception exception)
            {
                return new SaveSerializationResult(false, string.Empty, exception.Message);
            }
        }

        public SaveDeserializationResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SaveDeserializationResult(false, null, "Save data is empty.");
            }

            try
            {
                var versionProbe = JsonUtility.FromJson<SaveSchemaVersionProbe>(json);
                if (versionProbe == null)
                {
                    return new SaveDeserializationResult(false, null, "Save data could not be parsed.");
                }

                var saveData = versionProbe.schemaVersion switch
                {
                    PlayerProfileSaveData.CurrentSchemaVersion => JsonUtility.FromJson<PlayerProfileSaveData>(json),
                    1 => MigrateFromVersion1(JsonUtility.FromJson<PlayerProfileSaveData>(json)),
                    0 => MigrateFromVersion0(JsonUtility.FromJson<PlayerProfileSaveDataV0>(json)),
                    _ => null
                };

                return saveData == null
                    ? new SaveDeserializationResult(false, null, "Save schema version is unsupported.")
                    : new SaveDeserializationResult(true, saveData, string.Empty);
            }
            catch (Exception exception)
            {
                return new SaveDeserializationResult(false, null, exception.Message);
            }
        }

        private static PlayerProfileSaveData MigrateFromVersion0(PlayerProfileSaveDataV0 oldData)
        {
            if (oldData == null)
            {
                return null;
            }

            return new PlayerProfileSaveData
            {
                schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                currency = oldData.currency,
                engineUpgradeLevel = oldData.engineUpgradeLevel,
                handlingUpgradeLevel = oldData.handlingUpgradeLevel,
                completedRaceCount = 0,
                selectedVehicleId = oldData.selectedVehicleStableId
            };
        }

        private static PlayerProfileSaveData MigrateFromVersion1(PlayerProfileSaveData oldData)
        {
            if (oldData == null)
            {
                return null;
            }

            return new PlayerProfileSaveData
            {
                schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                currency = oldData.currency,
                engineUpgradeLevel = oldData.engineUpgradeLevel,
                handlingUpgradeLevel = oldData.handlingUpgradeLevel,
                completedRaceCount = 0,
                selectedVehicleId = oldData.selectedVehicleId
            };
        }
    }
}
