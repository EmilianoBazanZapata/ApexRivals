namespace ApexRivals.Settings.Runtime
{
    public interface ISettingsStorage
    {
        bool SettingsExist();
        SettingsStorageReadResult Load();
        SettingsStorageWriteResult Save(SettingsState settings);
        SettingsStorageWriteResult RestoreDefaults(SettingsState defaults);
    }
}
