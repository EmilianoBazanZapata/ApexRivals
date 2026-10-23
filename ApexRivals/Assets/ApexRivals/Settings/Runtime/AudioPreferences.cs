namespace ApexRivals.Settings.Runtime
{
    public readonly struct AudioPreferences
    {
        public AudioPreferences(float masterVolume, float musicVolume, float uiVolume, float vehicleVolume)
        {
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            UiVolume = uiVolume;
            VehicleVolume = vehicleVolume;
        }

        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float UiVolume { get; }
        public float VehicleVolume { get; }
    }
}
