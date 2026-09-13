using System;
using System.IO;
using UnityEngine;

namespace ApexRivals.SaveSystem.Runtime
{
    public sealed class PlayerProfileFileStorage : IPlayerProfileSaveStorage
    {
        public const string DefaultFileName = "player-profile.json";

        private readonly string _savePath;
        private readonly string _backupPath;
        private readonly string _temporaryPath;

        public PlayerProfileFileStorage(string fileName = DefaultFileName)
            : this(Application.persistentDataPath, fileName)
        {
        }

        public PlayerProfileFileStorage(string directoryPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Save directory cannot be empty.", nameof(directoryPath));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Save file name cannot be empty.", nameof(fileName));
            }

            _savePath = Path.Combine(directoryPath, fileName);
            _backupPath = _savePath + ".bak";
            _temporaryPath = _savePath + ".tmp";
        }

        public bool SaveExists()
        {
            return File.Exists(_savePath);
        }

        public bool BackupExists()
        {
            return File.Exists(_backupPath);
        }

        public SaveStorageReadResult ReadSave()
        {
            return ReadPath(_savePath);
        }

        public SaveStorageReadResult ReadBackup()
        {
            return ReadPath(_backupPath);
        }

        public SaveStorageWriteResult WriteSave(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return new SaveStorageWriteResult(false, "Save payload is empty.");
            }

            try
            {
                var directoryPath = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(_temporaryPath, payload);

                if (File.Exists(_savePath))
                {
                    TryDeletePath(_backupPath);
                    File.Replace(_temporaryPath, _savePath, _backupPath, true);
                    return new SaveStorageWriteResult(true, string.Empty);
                }

                File.Move(_temporaryPath, _savePath);
                return new SaveStorageWriteResult(true, string.Empty);
            }
            catch (Exception exception)
            {
                TryDeleteTemporaryFile();
                return new SaveStorageWriteResult(false, exception.Message);
            }
        }

        public SaveStorageWriteResult DeleteSave()
        {
            try
            {
                TryDeletePath(_savePath);
                TryDeletePath(_backupPath);
                TryDeleteTemporaryFile();
                return new SaveStorageWriteResult(true, string.Empty);
            }
            catch (Exception exception)
            {
                return new SaveStorageWriteResult(false, exception.Message);
            }
        }

        private static SaveStorageReadResult ReadPath(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new SaveStorageReadResult(false, string.Empty, "Save file does not exist.");
                }

                return new SaveStorageReadResult(true, File.ReadAllText(path), string.Empty);
            }
            catch (Exception exception)
            {
                return new SaveStorageReadResult(false, string.Empty, exception.Message);
            }
        }

        private void TryDeleteTemporaryFile()
        {
            TryDeletePath(_temporaryPath);
        }

        private static void TryDeletePath(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
