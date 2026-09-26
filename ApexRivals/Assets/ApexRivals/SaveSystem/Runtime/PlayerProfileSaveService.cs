using System;
using System.Collections.Generic;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;

namespace ApexRivals.SaveSystem.Runtime
{
    public sealed class PlayerProfileSaveService
    {
        private readonly PlayerProgressionState _progressionState;
        private readonly SelectedVehicleState _selectedVehicleState;
        private readonly IPlayerProfileSaveStorage _storage;
        private readonly PlayerProfileJsonSerializer _serializer;
        private readonly PlayerProfileSaveValidator _validator;

        public PlayerProfileSaveService(
            PlayerProgressionState progressionState,
            SelectedVehicleState selectedVehicleState,
            IPlayerProfileSaveStorage storage,
            IEnumerable<string> knownVehicleIds = null)
        {
            _progressionState = progressionState ?? throw new ArgumentNullException(nameof(progressionState));
            _selectedVehicleState = selectedVehicleState ?? throw new ArgumentNullException(nameof(selectedVehicleState));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = new PlayerProfileJsonSerializer();
            _validator = new PlayerProfileSaveValidator(SelectedVehicleState.DefaultVehicleId, knownVehicleIds);
        }

        public PlayerProfileSaveResult Save()
        {
            var saveData = Capture();
            var serializationResult = _serializer.Serialize(saveData);
            if (!serializationResult.Succeeded)
            {
                return new PlayerProfileSaveResult(PlayerProfileSaveStatus.SerializationFailed, serializationResult.Error);
            }

            var writeResult = _storage.WriteSave(serializationResult.Json);
            return writeResult.Succeeded
                ? new PlayerProfileSaveResult(PlayerProfileSaveStatus.Saved, string.Empty)
                : new PlayerProfileSaveResult(PlayerProfileSaveStatus.StorageFailed, writeResult.Error);
        }

        public PlayerProfileLoadResult LoadOrCreateDefault()
        {
            if (!_storage.SaveExists())
            {
                var defaultData = PlayerProfileSaveData.CreateDefault();
                Restore(defaultData);
                return new PlayerProfileLoadResult(PlayerProfileLoadStatus.CreatedDefault, defaultData, string.Empty);
            }

            var primaryResult = TryLoadFromStorage(_storage.ReadSave());
            if (primaryResult.Succeeded)
            {
                Restore(primaryResult.SaveData);
                return new PlayerProfileLoadResult(PlayerProfileLoadStatus.Loaded, primaryResult.SaveData, string.Empty);
            }

            if (_storage.BackupExists())
            {
                var backupResult = TryLoadFromStorage(_storage.ReadBackup());
                if (backupResult.Succeeded)
                {
                    Restore(backupResult.SaveData);
                    return new PlayerProfileLoadResult(PlayerProfileLoadStatus.RecoveredFromBackup, backupResult.SaveData, primaryResult.Message);
                }
            }

            return new PlayerProfileLoadResult(PlayerProfileLoadStatus.Failed, null, primaryResult.Message);
        }

        public PlayerProfileSaveData Capture()
        {
            return new PlayerProfileSaveData
            {
                schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                currency = _progressionState.Currency,
                engineUpgradeLevel = _progressionState.GetUpgradeLevel(UpgradeType.Engine),
                handlingUpgradeLevel = _progressionState.GetUpgradeLevel(UpgradeType.Handling),
                completedRaceCount = _progressionState.CompletedRaceCount,
                selectedVehicleId = _selectedVehicleState.SelectedVehicleId,
                vehicleSelectionCommitted = _selectedVehicleState.HasCommittedVehicleSelection
            };
        }

        public PlayerProfileSaveResult ResetProfile()
        {
            var deleteResult = _storage.DeleteSave();
            if (!deleteResult.Succeeded)
            {
                return new PlayerProfileSaveResult(PlayerProfileSaveStatus.StorageFailed, deleteResult.Error);
            }

            Restore(PlayerProfileSaveData.CreateDefault());
            return new PlayerProfileSaveResult(PlayerProfileSaveStatus.Deleted, string.Empty);
        }

        private PlayerProfileLoadResult TryLoadFromStorage(SaveStorageReadResult readResult)
        {
            if (!readResult.Succeeded)
            {
                return new PlayerProfileLoadResult(PlayerProfileLoadStatus.Failed, null, readResult.Error);
            }

            var deserializationResult = _serializer.Deserialize(readResult.Payload);
            if (!deserializationResult.Succeeded)
            {
                return new PlayerProfileLoadResult(PlayerProfileLoadStatus.Failed, null, deserializationResult.Error);
            }

            var validationResult = _validator.Validate(deserializationResult.SaveData);
            if (!validationResult.IsValid)
            {
                return new PlayerProfileLoadResult(PlayerProfileLoadStatus.Failed, null, validationResult.Message);
            }

            return new PlayerProfileLoadResult(PlayerProfileLoadStatus.Loaded, validationResult.SaveData, string.Empty);
        }

        private void Restore(PlayerProfileSaveData saveData)
        {
            _progressionState.SetCurrency(saveData.currency);
            _progressionState.SetUpgradeLevel(UpgradeType.Engine, saveData.engineUpgradeLevel);
            _progressionState.SetUpgradeLevel(UpgradeType.Handling, saveData.handlingUpgradeLevel);
            _progressionState.SetCompletedRaceCount(saveData.completedRaceCount);
            _selectedVehicleState.Restore(saveData.selectedVehicleId, saveData.vehicleSelectionCommitted);
        }
    }
}
