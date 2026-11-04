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

                if (versionProbe.schemaVersion > PlayerProfileSaveData.CurrentSchemaVersion)
                {
                    return new SaveDeserializationResult(false, null, "Save schema version is unsupported.", true);
                }

                if (versionProbe.schemaVersion != PlayerProfileSaveData.CurrentSchemaVersion
                    && versionProbe.schemaVersion != PlayerProfileSaveData.PreviousSchemaVersion)
                {
                    return new SaveDeserializationResult(false, null, "Save schema version is unsupported.");
                }

                var saveData = JsonUtility.FromJson<PlayerProfileSaveData>(json);
                if (saveData == null)
                {
                    return new SaveDeserializationResult(false, null, "Save schema version is unsupported.");
                }

                if (versionProbe.schemaVersion == PlayerProfileSaveData.PreviousSchemaVersion)
                {
                    MigrateFromPreCareerSchema(saveData);
                }

                return new SaveDeserializationResult(true, saveData, string.Empty);
            }
            catch (Exception exception)
            {
                return new SaveDeserializationResult(false, null, exception.Message);
            }
        }

        /// <summary>
        /// Schema 3 had no career. Currency, upgrades, race count and the selected car carry over unchanged.
        /// Every car was raceable then, so a committed car stays owned (the starter is granted by the career on
        /// load). No per-track completion, win or milestone is invented from the old race count.
        /// </summary>
        private static void MigrateFromPreCareerSchema(PlayerProfileSaveData saveData)
        {
            var owned = saveData.vehicleSelectionCommitted && !string.IsNullOrWhiteSpace(saveData.selectedVehicleId)
                ? new[] { saveData.selectedVehicleId.Trim() }
                : Array.Empty<string>();
            saveData.career = new CareerSaveData { ownedVehicleIds = owned };
            saveData.schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion;
        }
    }
}
