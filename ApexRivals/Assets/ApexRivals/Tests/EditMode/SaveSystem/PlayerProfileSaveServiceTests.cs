using System.Collections.Generic;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.SaveSystem
{
    public sealed class PlayerProfileSaveServiceTests
    {
        [Test]
        public void LoadOrCreateDefault_WhenSaveDoesNotExist_CreatesNewProfileDefaults()
        {
            var progression = new PlayerProgressionState(50);
            var selectedVehicle = new SelectedVehicleState("sport", true);
            var storage = new MemoryProfileSaveStorage();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.CreatedDefault));
            Assert.That(progression.Currency, Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(0));
            Assert.That(progression.CompletedRaceCount, Is.Zero);
            Assert.That(selectedVehicle.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicle.SelectedVehicleId, Is.Empty);
        }

        [Test]
        public void SaveAndLoad_RoundTrip_RestoresProfile()
        {
            var storage = new MemoryProfileSaveStorage();
            var progression = CreateProgression(275, 1, 2, 4);
            var selectedVehicle = new SelectedVehicleState("sport", true);
            var service = CreateService(progression, selectedVehicle, storage);
            service.Save();

            var restoredProgression = new PlayerProgressionState();
            var restoredVehicle = new SelectedVehicleState();
            var restoreService = CreateService(restoredProgression, restoredVehicle, storage);

            var result = restoreService.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded));
            Assert.That(restoredProgression.Currency, Is.EqualTo(275));
            Assert.That(restoredProgression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
            Assert.That(restoredProgression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(2));
            Assert.That(restoredProgression.CompletedRaceCount, Is.EqualTo(4));
            Assert.That(restoredVehicle.HasCommittedVehicleSelection, Is.True);
            Assert.That(restoredVehicle.SelectedVehicleId, Is.EqualTo("sport"));
        }

        [Test]
        public void Save_UsesTheEstablishedCurrentSchemaJsonContract()
        {
            var storage = new MemoryProfileSaveStorage();
            var service = CreateService(
                CreateProgression(275, 1, 2, 4),
                new SelectedVehicleState("sport", true),
                storage);

            var result = service.Save();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileSaveStatus.Saved));
            Assert.That(storage.SavePayload, Does.Contain("\"schemaVersion\""));
            Assert.That(storage.SavePayload, Does.Contain("\"currency\""));
            Assert.That(storage.SavePayload, Does.Contain("\"engineUpgradeLevel\""));
            Assert.That(storage.SavePayload, Does.Contain("\"handlingUpgradeLevel\""));
            Assert.That(storage.SavePayload, Does.Contain("\"completedRaceCount\""));
            Assert.That(storage.SavePayload, Does.Contain("\"selectedVehicleId\""));
            Assert.That(storage.SavePayload, Does.Contain("\"vehicleSelectionCommitted\""));

            var deserializationResult = new PlayerProfileJsonSerializer().Deserialize(storage.SavePayload);
            Assert.That(deserializationResult.Succeeded, Is.True);
            Assert.That(deserializationResult.SaveData.schemaVersion, Is.EqualTo(PlayerProfileSaveData.CurrentSchemaVersion));
            Assert.That(deserializationResult.SaveData.selectedVehicleId, Is.EqualTo("sport"));
        }

        [Test]
        public void LoadOrCreateDefault_NegativeCurrency_FailsWithoutMutation()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = -1,
                    engineUpgradeLevel = 1,
                    handlingUpgradeLevel = 1,
                    completedRaceCount = 1,
                    selectedVehicleId = "sport",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = CreateProgression(25, 0, 0);
            var selectedVehicle = new SelectedVehicleState("starter", true);
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(progression.Currency, Is.EqualTo(25));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
        }

        [Test]
        public void LoadOrCreateDefault_NegativeUpgradeLevel_FailsWithoutMutation()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 10,
                    engineUpgradeLevel = -1,
                    handlingUpgradeLevel = 0,
                    completedRaceCount = 1,
                    selectedVehicleId = "sport",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = CreateProgression(25, 1, 1);
            var selectedVehicle = new SelectedVehicleState("starter", true);
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(progression.Currency, Is.EqualTo(25));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
        }

        [Test]
        public void LoadOrCreateDefault_UnknownCommittedVehicleId_RequiresSelection()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 10,
                    engineUpgradeLevel = 0,
                    handlingUpgradeLevel = 0,
                    completedRaceCount = 0,
                    selectedVehicleId = "missing",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded));
            Assert.That(selectedVehicle.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicle.SelectedVehicleId, Is.Empty);
        }

        [Test]
        public void LoadOrCreateDefault_EmptySave_Fails()
        {
            var storage = new MemoryProfileSaveStorage { SavePayload = string.Empty };
            var service = CreateService(new PlayerProgressionState(), new SelectedVehicleState(), storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
        }

        [Test]
        public void LoadOrCreateDefault_MalformedJson_Fails()
        {
            var storage = new MemoryProfileSaveStorage { SavePayload = "{ invalid json" };
            var service = CreateService(new PlayerProgressionState(), new SelectedVehicleState(), storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
        }

        [Test]
        public void LoadOrCreateDefault_PrimaryCorruptBackupValid_RecoversFromBackup()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = "{ invalid json",
                BackupPayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 90,
                    engineUpgradeLevel = 1,
                    handlingUpgradeLevel = 0,
                    completedRaceCount = 3,
                    selectedVehicleId = "sport",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.RecoveredFromBackup));
            Assert.That(progression.Currency, Is.EqualTo(90));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
            Assert.That(progression.CompletedRaceCount, Is.EqualTo(3));
            Assert.That(selectedVehicle.HasCommittedVehicleSelection, Is.True);
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("sport"));
            Assert.That(storage.SavePayload, Is.EqualTo("{ invalid json"));
        }

        [Test]
        public void LoadOrCreateDefault_PrimaryReadFailureBackupValid_RecoversFromBackup()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = "unreadable primary payload",
                SaveReadError = "Primary save could not be read.",
                BackupPayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 90,
                    engineUpgradeLevel = 1,
                    handlingUpgradeLevel = 0,
                    completedRaceCount = 3,
                    selectedVehicleId = "sport",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.RecoveredFromBackup));
            Assert.That(result.Message, Is.EqualTo("Primary save could not be read."));
            Assert.That(progression.Currency, Is.EqualTo(90));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("sport"));
        }

        [Test]
        public void LoadOrCreateDefault_BothPrimaryAndBackupInvalid_FailsWithoutAwardingProgress()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = "{ invalid json",
                BackupPayload = "{ also invalid"
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(progression.Currency, Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(0));
            Assert.That(progression.CompletedRaceCount, Is.Zero);
        }

        [Test]
        public void LoadOrCreateDefault_UnsupportedFutureSchemaVersion_Fails()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = "{ \"schemaVersion\": 99, \"currency\": 10 }"
            };
            var service = CreateService(new PlayerProgressionState(), new SelectedVehicleState(), storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(result.Message, Is.EqualTo("Save schema version is unsupported."));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void LoadOrCreateDefault_LegacySchemaVersion_FailsWithoutMigrating(int schemaVersion)
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = $"{{ \"schemaVersion\": {schemaVersion}, \"currency\": 125, \"engineUpgradeLevel\": 2, \"handlingUpgradeLevel\": 1, \"completedRaceCount\": 3, \"selectedVehicleId\": \"sport\", \"vehicleSelectionCommitted\": true }}"
            };
            var progression = CreateProgression(25, 0, 0, 1);
            var selectedVehicle = new SelectedVehicleState("starter", true);
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(result.Message, Is.EqualTo("Save schema version is unsupported."));
            Assert.That(progression.Currency, Is.EqualTo(25));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
        }

        [Test]
        public void LoadOrCreateDefault_LegacyPrimaryCurrentBackup_RecoversFromBackup()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = "{ \"schemaVersion\": 2, \"currency\": 125 }",
                BackupPayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 90,
                    engineUpgradeLevel = 1,
                    handlingUpgradeLevel = 0,
                    completedRaceCount = 3,
                    selectedVehicleId = "sport",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.RecoveredFromBackup));
            Assert.That(result.Message, Is.EqualTo("Save schema version is unsupported."));
            Assert.That(progression.Currency, Is.EqualTo(90));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("sport"));
        }

        [Test]
        public void LoadOrCreateDefault_LegacyPrimaryAndBackup_FailsWithoutMutation()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = "{ \"schemaVersion\": 2, \"currency\": 125 }",
                BackupPayload = "{ \"schemaVersion\": 1, \"currency\": 90 }"
            };
            var progression = CreateProgression(25, 1, 1, 2);
            var selectedVehicle = new SelectedVehicleState("starter", true);
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(result.Message, Is.EqualTo("Save schema version is unsupported."));
            Assert.That(progression.Currency, Is.EqualTo(25));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
        }

        [Test]
        public void LoadOrCreateDefault_NegativeCompletedRaceCount_FailsWithoutMutation()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 10,
                    engineUpgradeLevel = 0,
                    handlingUpgradeLevel = 0,
                    completedRaceCount = -1,
                    selectedVehicleId = "sport",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = CreateProgression(25, 1, 1, 3);
            var selectedVehicle = new SelectedVehicleState("starter", true);
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(progression.CompletedRaceCount, Is.EqualTo(3));
            Assert.That(progression.Currency, Is.EqualTo(25));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
        }

        [Test]
        public void Save_WhenStorageWriteFails_DoesNotDestroyPreviousValidSave()
        {
            var previousPayload = Serialize(new PlayerProfileSaveData
            {
                schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                currency = 15,
                engineUpgradeLevel = 0,
                handlingUpgradeLevel = 0,
                completedRaceCount = 1,
                selectedVehicleId = "starter",
                vehicleSelectionCommitted = true
            });
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = previousPayload,
                FailWrites = true
            };
            var progression = CreateProgression(100, 1, 1);
            var service = CreateService(progression, new SelectedVehicleState("sport", true), storage);

            var result = service.Save();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileSaveStatus.StorageFailed));
            Assert.That(storage.SavePayload, Is.EqualTo(previousPayload));
        }

        [Test]
        public void LoadOrCreateDefault_InvalidPrimaryDoesNotMutateBeforeBackupValidationSucceeds()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = -10,
                    engineUpgradeLevel = 2,
                    handlingUpgradeLevel = 2,
                    completedRaceCount = 2,
                    selectedVehicleId = "sport",
                    vehicleSelectionCommitted = true
                }),
                BackupPayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 45,
                    engineUpgradeLevel = 0,
                    handlingUpgradeLevel = 1,
                    completedRaceCount = 5,
                    selectedVehicleId = "starter",
                    vehicleSelectionCommitted = true
                })
            };
            var progression = CreateProgression(5, 0, 0);
            var selectedVehicle = new SelectedVehicleState("starter", true);
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.RecoveredFromBackup));
            Assert.That(progression.Currency, Is.EqualTo(45));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(1));
            Assert.That(progression.CompletedRaceCount, Is.EqualTo(5));
            Assert.That(selectedVehicle.HasCommittedVehicleSelection, Is.True);
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
        }

        [Test]
        public void ResetProfile_ClearsProgressionAndCommitment()
        {
            var storage = new MemoryProfileSaveStorage { SavePayload = "{}" };
            var progression = CreateProgression(275, 1, 2, 4);
            var selectedVehicle = new SelectedVehicleState("sport", true);
            var service = CreateService(progression, selectedVehicle, storage);
            var resetService = new PlayerProfileResetService(service);

            var result = resetService.ResetPlayerProfile();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileSaveStatus.Deleted));
            Assert.That(storage.SaveExists(), Is.False);
            Assert.That(storage.BackupExists(), Is.False);
            Assert.That(progression.Currency, Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(0));
            Assert.That(progression.CompletedRaceCount, Is.EqualTo(0));
            Assert.That(selectedVehicle.HasCommittedVehicleSelection, Is.False);
            Assert.That(selectedVehicle.SelectedVehicleId, Is.Empty);
        }

        private static PlayerProfileSaveService CreateService(
            PlayerProgressionState progression,
            SelectedVehicleState selectedVehicle,
            IPlayerProfileSaveStorage storage)
        {
            return new PlayerProfileSaveService(
                progression,
                selectedVehicle,
                storage,
                new[] { SelectedVehicleState.DefaultVehicleId, "sport" });
        }

        private static PlayerProgressionState CreateProgression(int currency, int engineLevel, int handlingLevel)
        {
            return CreateProgression(currency, engineLevel, handlingLevel, 0);
        }

        private static PlayerProgressionState CreateProgression(int currency, int engineLevel, int handlingLevel, int completedRaceCount)
        {
            var progression = new PlayerProgressionState(currency, completedRaceCount);
            progression.SetUpgradeLevel(UpgradeType.Engine, engineLevel);
            progression.SetUpgradeLevel(UpgradeType.Handling, handlingLevel);
            return progression;
        }

        private static string Serialize(PlayerProfileSaveData saveData)
        {
            var serializer = new PlayerProfileJsonSerializer();
            var result = serializer.Serialize(saveData);
            Assert.That(result.Succeeded, Is.True);
            return result.Json;
        }

        private sealed class MemoryProfileSaveStorage : IPlayerProfileSaveStorage
        {
            public string SavePayload { get; set; }
            public string BackupPayload { get; set; }
            public string SaveReadError { get; set; }
            public bool FailWrites { get; set; }

            public bool SaveExists()
            {
                return SavePayload != null;
            }

            public bool BackupExists()
            {
                return BackupPayload != null;
            }

            public SaveStorageReadResult ReadSave()
            {
                if (!string.IsNullOrEmpty(SaveReadError))
                {
                    return new SaveStorageReadResult(false, string.Empty, SaveReadError);
                }

                return SaveExists()
                    ? new SaveStorageReadResult(true, SavePayload, string.Empty)
                    : new SaveStorageReadResult(false, string.Empty, "Save file does not exist.");
            }

            public SaveStorageReadResult ReadBackup()
            {
                return BackupExists()
                    ? new SaveStorageReadResult(true, BackupPayload, string.Empty)
                    : new SaveStorageReadResult(false, string.Empty, "Backup file does not exist.");
            }

            public SaveStorageWriteResult WriteSave(string payload)
            {
                if (FailWrites)
                {
                    return new SaveStorageWriteResult(false, "Write failed.");
                }

                if (SavePayload != null)
                {
                    BackupPayload = SavePayload;
                }

                SavePayload = payload;
                return new SaveStorageWriteResult(true, string.Empty);
            }

            public SaveStorageWriteResult DeleteSave()
            {
                SavePayload = null;
                BackupPayload = null;
                return new SaveStorageWriteResult(true, string.Empty);
            }
        }
    }
}
