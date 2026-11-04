using System;

namespace ApexRivals.Audio.Runtime
{
    public enum UiSound
    {
        /// <summary>Focus moved to another control (keyboard, gamepad or hover), or a slider stepped.</summary>
        Move,
        /// <summary>A button was pressed.</summary>
        Confirm,
        /// <summary>Back / cancel.</summary>
        Back
    }

    /// <summary>Decouples UI widgets from the audio player: widgets raise sounds, <see cref="GameAudio"/> plays them.</summary>
    public static class UiAudioEvents
    {
        public static event Action<UiSound> Played;

        public static void Raise(UiSound sound)
        {
            Played?.Invoke(sound);
        }
    }
}
