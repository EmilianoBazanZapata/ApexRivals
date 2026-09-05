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
                var result = storage.WriteSave("{ \"schemaVersion\": 1 }");

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
    }
}
