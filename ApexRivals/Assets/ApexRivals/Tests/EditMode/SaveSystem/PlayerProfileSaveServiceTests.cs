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
            var selectedVehicle = new SelectedVehicleState("sport");
            var storage = new MemoryProfileSaveStorage();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.CreatedDefault));
            Assert.That(progression.Currency, Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(0));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo(SelectedVehicleState.DefaultVehicleId));
        }

        [Test]
        public void SaveAndLoad_RoundTrip_RestoresProfile()
        {
            var storage = new MemoryProfileSaveStorage();
            var progression = CreateProgression(275, 1, 2);
            var selectedVehicle = new SelectedVehicleState("sport");
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
            Assert.That(restoredVehicle.SelectedVehicleId, Is.EqualTo("sport"));
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
                    selectedVehicleId = "sport"
                })
            };
            var progression = CreateProgression(25, 0, 0);
            var selectedVehicle = new SelectedVehicleState("starter");
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
                    selectedVehicleId = "sport"
                })
            };
            var progression = CreateProgression(25, 1, 1);
            var selectedVehicle = new SelectedVehicleState("starter");
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(progression.Currency, Is.EqualTo(25));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
        }

        [Test]
        public void LoadOrCreateDefault_UnknownVehicleId_FallsBackSafely()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 10,
                    engineUpgradeLevel = 0,
                    handlingUpgradeLevel = 0,
                    selectedVehicleId = "missing"
                })
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo(SelectedVehicleState.DefaultVehicleId));
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
                    selectedVehicleId = "sport"
                })
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.RecoveredFromBackup));
            Assert.That(progression.Currency, Is.EqualTo(90));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("sport"));
            Assert.That(storage.SavePayload, Is.EqualTo("{ invalid json"));
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
        }

        [Test]
        public void LoadOrCreateDefault_OlderSchemaFixture_MigratesSequentially()
        {
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = "{ \"schemaVersion\": 0, \"currency\": 125, \"engineUpgradeLevel\": 2, \"handlingUpgradeLevel\": 1, \"selectedVehicleStableId\": \"sport\" }"
            };
            var progression = new PlayerProgressionState();
            var selectedVehicle = new SelectedVehicleState();
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded));
            Assert.That(result.SaveData.schemaVersion, Is.EqualTo(PlayerProfileSaveData.CurrentSchemaVersion));
            Assert.That(progression.Currency, Is.EqualTo(125));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(2));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(1));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("sport"));
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
                selectedVehicleId = "starter"
            });
            var storage = new MemoryProfileSaveStorage
            {
                SavePayload = previousPayload,
                FailWrites = true
            };
            var progression = CreateProgression(100, 1, 1);
            var service = CreateService(progression, new SelectedVehicleState("sport"), storage);

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
                    selectedVehicleId = "sport"
                }),
                BackupPayload = Serialize(new PlayerProfileSaveData
                {
                    schemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                    currency = 45,
                    engineUpgradeLevel = 0,
                    handlingUpgradeLevel = 1,
                    selectedVehicleId = "starter"
                })
            };
            var progression = CreateProgression(5, 0, 0);
            var selectedVehicle = new SelectedVehicleState("starter");
            var service = CreateService(progression, selectedVehicle, storage);

            var result = service.LoadOrCreateDefault();

            Assert.That(result.Status, Is.EqualTo(PlayerProfileLoadStatus.RecoveredFromBackup));
            Assert.That(progression.Currency, Is.EqualTo(45));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(0));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(1));
            Assert.That(selectedVehicle.SelectedVehicleId, Is.EqualTo("starter"));
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
            var progression = new PlayerProgressionState(currency);
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
