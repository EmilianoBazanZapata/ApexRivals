namespace ApexRivals.SaveSystem.Runtime
{
    public interface IPlayerProfileSaveStorage
    {
        bool SaveExists();
        bool BackupExists();
        SaveStorageReadResult ReadSave();
        SaveStorageReadResult ReadBackup();
        // Preserve the validated recovery copy while replacing a missing/invalid primary.
        // On failure, leave existing primary and backup files intact.
        SaveStorageWriteResult WriteSave(string payload, bool preserveBackup = false);
        SaveStorageWriteResult DeleteSave();
    }
}
