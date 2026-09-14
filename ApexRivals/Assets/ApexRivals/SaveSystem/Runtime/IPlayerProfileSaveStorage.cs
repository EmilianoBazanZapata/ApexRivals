namespace ApexRivals.SaveSystem.Runtime
{
    public interface IPlayerProfileSaveStorage
    {
        bool SaveExists();
        bool BackupExists();
        SaveStorageReadResult ReadSave();
        SaveStorageReadResult ReadBackup();
        SaveStorageWriteResult WriteSave(string payload);
        SaveStorageWriteResult DeleteSave();
    }
}
