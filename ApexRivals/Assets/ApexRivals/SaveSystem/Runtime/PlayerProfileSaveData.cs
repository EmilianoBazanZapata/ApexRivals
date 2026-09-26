using System;
using ApexRivals.Garage.Runtime;

namespace ApexRivals.SaveSystem.Runtime
{
    [Serializable]
    public sealed class PlayerProfileSaveData
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public int currency;
        public int engineUpgradeLevel;
        public int handlingUpgradeLevel;
        public int completedRaceCount;
        public string selectedVehicleId = string.Empty;
        public bool vehicleSelectionCommitted;

        public static PlayerProfileSaveData CreateDefault()
        {
            return new PlayerProfileSaveData
            {
                schemaVersion = CurrentSchemaVersion,
                currency = 0,
                engineUpgradeLevel = 0,
                handlingUpgradeLevel = 0,
                completedRaceCount = 0,
                selectedVehicleId = string.Empty,
                vehicleSelectionCommitted = false
            };
        }
    }
}
