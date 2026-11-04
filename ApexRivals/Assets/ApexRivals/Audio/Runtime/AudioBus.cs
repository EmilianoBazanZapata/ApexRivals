namespace ApexRivals.Audio.Runtime
{
    /// <summary>Sub-mixes under the master volume; each has its own slider in Settings &gt; Audio except ambience.</summary>
    public enum AudioBus
    {
        Music,
        Ui,
        Vehicles,
        /// <summary>Crowd, wind and rain: follows the master slider only.</summary>
        Ambience
    }
}
