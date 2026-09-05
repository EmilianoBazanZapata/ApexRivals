using System;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.Bootstrap.Runtime
{
    public sealed class PlayerProfileSaveCheckpoint : ISaveCheckpoint, IVehicleSelectionSaveCheckpoint
    {
        private readonly PlayerProfileSaveService _saveService;

        public PlayerProfileSaveCheckpoint(PlayerProfileSaveService saveService)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        }

        public SaveCheckpointResult Save()
        {
            var saveResult = _saveService.Save();
            return saveResult.Succeeded
                ? SaveCheckpointResult.Success()
                : SaveCheckpointResult.Failure(saveResult.Message);
        }

        VehicleSelectionSaveResult IVehicleSelectionSaveCheckpoint.Save()
        {
            var saveResult = _saveService.Save();
            return saveResult.Succeeded
                ? VehicleSelectionSaveResult.Success()
                : VehicleSelectionSaveResult.Failure(saveResult.Message);
        }
    }
}
