namespace ApexRivals.Settings.Runtime
{
    public readonly struct AudioPreferences
    {
        public AudioPreferences(float masterVolume, float musicVolume, float uiVolume, float vehicleVolume, float ambienceVolume = 1f)
        {
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            UiVolume = uiVolume;
            VehicleVolume = vehicleVolume;
            AmbienceVolume = ambienceVolume;
        }

        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float UiVolume { get; }
        public float VehicleVolume { get; }
        public float AmbienceVolume { get; }
    }
}
