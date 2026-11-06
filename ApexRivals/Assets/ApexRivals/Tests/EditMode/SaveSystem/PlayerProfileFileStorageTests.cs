using System;
using System.IO;
using ApexRivals.SaveSystem.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.SaveSystem
{
    public sealed class PlayerProfileFileStorageTests
    {
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
