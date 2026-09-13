namespace ApexRivals.Settings.Runtime
{
    public readonly struct AudioPreferences
    {
        public AudioPreferences(float masterVolume, float musicVolume, float sfxVolume)
        {
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
        }

        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
    }
}
