using System;
using System.IO;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.SaveSystem
{
    public sealed class PlayerProfileFileStorageTests
    {
        [Test]
        public void ResetProfileDeletesPrimaryBackupAndTemporaryFiles()
        {
            InTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "profile.json");
                File.WriteAllText(path, "primary");
                File.WriteAllText(path + ".bak", "backup");
                File.WriteAllText(path + ".tmp", "temporary");
                var progression = new PlayerProgressionState(275, completedRaceCount: 4);
                var selected = new SelectedVehicleState("sport", true);
                var service = new PlayerProfileSaveService(progression, selected, new PlayerProfileFileStorage(directory, "profile.json"));

                var result = service.ResetProfile();

                Assert.That(result.Status, Is.EqualTo(PlayerProfileSaveStatus.Deleted));
                Assert.That(File.Exists(path), Is.False);
                Assert.That(File.Exists(path + ".bak"), Is.False);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
                Assert.That(progression.Currency, Is.Zero);
                Assert.That(progression.CompletedRaceCount, Is.Zero);
                Assert.That(selected.HasCommittedVehicleSelection, Is.False);
            });
        }

        [Test, Platform("Win")]
        public void ResetProfileWhenPrimaryIsLockedPreservesProfileAndCanRetry()
        {
            InTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "profile.json");
                File.WriteAllText(path, "primary");
                File.WriteAllText(path + ".bak", "backup");
                File.WriteAllText(path + ".tmp", "temporary");
                var progression = new PlayerProgressionState(275, completedRaceCount: 4);
                progression.SetUpgradeLevel(UpgradeType.Engine, 1);
                progression.SetUpgradeLevel(UpgradeType.Handling, 2);
                var selected = new SelectedVehicleState("sport", true);
                var service = new PlayerProfileSaveService(progression, selected, new PlayerProfileFileStorage(directory, "profile.json"));

                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var result = service.ResetProfile();

                    Assert.That(result.Status, Is.EqualTo(PlayerProfileSaveStatus.StorageFailed));
                    Assert.That(result.Message, Is.Not.Empty);
                    Assert.That(progression.Currency, Is.EqualTo(275));
                    Assert.That(progression.CompletedRaceCount, Is.EqualTo(4));
                    Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
                    Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(2));
                    Assert.That(selected.SelectedVehicleId, Is.EqualTo("sport"));
                    Assert.That(selected.HasCommittedVehicleSelection, Is.True);
                    Assert.That(File.ReadAllText(path), Is.EqualTo("primary"));
                    Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("backup"));
                    Assert.That(File.ReadAllText(path + ".tmp"), Is.EqualTo("temporary"));
                }

                Assert.That(service.ResetProfile().Status, Is.EqualTo(PlayerProfileSaveStatus.Deleted));
                Assert.That(progression.Currency, Is.Zero);
                Assert.That(progression.CompletedRaceCount, Is.Zero);
                Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.Zero);
                Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.Zero);
                Assert.That(selected.HasCommittedVehicleSelection, Is.False);
                Assert.That(File.Exists(path), Is.False);
                Assert.That(File.Exists(path + ".bak"), Is.False);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            });
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SavingRecoveredProfileKeepsValidatedBackupThenResumesNormalRotation(int damagedPrimary)
        {
            InTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "profile.json");
                var data = PlayerProfileSaveData.CreateDefault();
                data.currency = 50;
                var backup = new PlayerProfileJsonSerializer().Serialize(data).Json;
                File.WriteAllText(path + ".bak", backup);
                if (damagedPrimary == 0) File.WriteAllText(path, "{ invalid json");
                if (damagedPrimary == 1)
                {
                    data.currency = -1;
                    File.WriteAllText(path, new PlayerProfileJsonSerializer().Serialize(data).Json);
                }

                var state = new PlayerProgressionState();
                var storage = new PlayerProfileFileStorage(directory, "profile.json");
                var service = new PlayerProfileSaveService(state, new SelectedVehicleState(), storage);
                Assert.That(service.LoadOrCreateDefault().Status, Is.EqualTo(PlayerProfileLoadStatus.RecoveredFromBackup));
                state.AddCurrency(10);
                Assert.That(service.Save().Succeeded, Is.True);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
                AssertValidCurrency(storage.ReadSave(), 60);
                AssertValidCurrency(storage.ReadBackup(), 50);

                state.AddCurrency(20);
                Assert.That(service.Save().Succeeded, Is.True);
                AssertValidCurrency(storage.ReadSave(), 80);
                AssertValidCurrency(storage.ReadBackup(), 60);
            });
        }

        [Test, Platform("Win")]
        public void FailedReplacementDoesNotDeleteExistingBackup()
        {
            InTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "profile.json");
                var storage = new PlayerProfileFileStorage(directory, "profile.json");
                Assert.That(storage.WriteSave("first").Succeeded, Is.True);
                Assert.That(storage.WriteSave("second").Succeeded, Is.True);
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.That(storage.WriteSave("third").Succeeded, Is.False);
                    Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("first"));
                    Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
                }

                Assert.That(File.Exists(path + ".tmp"), Is.False);
            });
        }

        [Test, Platform("Win")]
        public void FailedSaveAfterRecoveryPreservesBackupAndRetriesWithoutRotatingCorruption()
        {
            InTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "profile.json");
                var data = PlayerProfileSaveData.CreateDefault();
                data.currency = 321;
                var backup = new PlayerProfileJsonSerializer().Serialize(data).Json;
                File.WriteAllText(path, "{ corrupt");
                File.WriteAllText(path + ".bak", backup);
                var storage = new PlayerProfileFileStorage(directory, "profile.json");
                var service = new PlayerProfileSaveService(new PlayerProgressionState(), new SelectedVehicleState(), storage);
                Assert.That(service.LoadOrCreateDefault().Succeeded, Is.True);
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.That(service.Save().Succeeded, Is.False);
                    Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
                }

                Assert.That(service.Save().Succeeded, Is.True);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
                AssertValidCurrency(storage.ReadSave(), 321);
                AssertValidCurrency(storage.ReadBackup(), 321);
            });
        }

        private static void AssertValidCurrency(SaveStorageReadResult read, int currency)
        {
            Assert.That(read.Succeeded, Is.True, read.Error);
            var deserialized = new PlayerProfileJsonSerializer().Deserialize(read.Payload);
            Assert.That(deserialized.Succeeded, Is.True, deserialized.Error);
            Assert.That(new PlayerProfileSaveValidator(SelectedVehicleState.DefaultVehicleId).Validate(deserialized.SaveData).IsValid, Is.True);
            Assert.That(deserialized.SaveData.currency, Is.EqualTo(currency));
        }

        private static void InTemporaryDirectory(Action<string> test)
        {
            var directory = Path.Combine(Path.GetTempPath(), "ApexRivalsSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { test(directory); }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void WriteSave_TemporaryDirectoryStorage_DoesNotUsePersistentDataPath()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "ApexRivalsSaveTests", Guid.NewGuid().ToString("N"));
            var storage = new PlayerProfileFileStorage(directoryPath, "profile.json");

            try
            {
                var result = storage.WriteSave($"{{ \"schemaVersion\": {PlayerProfileSaveData.CurrentSchemaVersion} }}");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(storage.SaveExists(), Is.True);
                Assert.That(File.Exists(Path.Combine(directoryPath, "profile.json")), Is.True);
            }
            finally
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }
            }
        }

        [Test]
        public void WriteSave_RepeatedWrites_ReplacesExistingBackup()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "ApexRivalsSaveTests", Guid.NewGuid().ToString("N"));
            var storage = new PlayerProfileFileStorage(directoryPath, "profile.json");

            try
            {
                Assert.That(storage.WriteSave($"{{ \"schemaVersion\": {PlayerProfileSaveData.CurrentSchemaVersion}, \"currency\": 1 }}").Succeeded, Is.True);
                Assert.That(storage.WriteSave($"{{ \"schemaVersion\": {PlayerProfileSaveData.CurrentSchemaVersion}, \"currency\": 2 }}").Succeeded, Is.True);

                var result = storage.WriteSave($"{{ \"schemaVersion\": {PlayerProfileSaveData.CurrentSchemaVersion}, \"currency\": 3 }}");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(File.ReadAllText(Path.Combine(directoryPath, "profile.json")), Does.Contain("\"currency\": 3"));
                Assert.That(File.Exists(Path.Combine(directoryPath, "profile.json.bak")), Is.True);
            }
            finally
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }
            }
        }
    }
}
