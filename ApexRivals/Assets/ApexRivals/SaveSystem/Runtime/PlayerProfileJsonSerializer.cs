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

                if (versionProbe.schemaVersion != PlayerProfileSaveData.CurrentSchemaVersion)
                {
                    return new SaveDeserializationResult(false, null, "Save schema version is unsupported.");
                }

                var saveData = JsonUtility.FromJson<PlayerProfileSaveData>(json);

                return saveData == null
                    ? new SaveDeserializationResult(false, null, "Save schema version is unsupported.")
                    : new SaveDeserializationResult(true, saveData, string.Empty);
            }
            catch (Exception exception)
            {
                return new SaveDeserializationResult(false, null, exception.Message);
            }
        }
    }
}
