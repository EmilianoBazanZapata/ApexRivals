using System;

namespace ApexRivals.SaveSystem.Runtime
{
    [Serializable]
    internal sealed class PlayerProfileSaveDataV0
    {
        public int schemaVersion;
        public int currency;
        public int engineUpgradeLevel;
        public int handlingUpgradeLevel;
        public string selectedVehicleStableId;
    }
}
