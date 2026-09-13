namespace ApexRivals.UI.Runtime
{
    public readonly struct RaceHudViewModel
    {
        public RaceHudViewModel(
            CountdownDisplayState countdownState,
            float countdownValue,
            int currentLap,
            int totalLaps,
            int currentPosition,
            int participantCount,
            float elapsedRaceTime,
            bool racingInputActive)
        {
            CountdownState = countdownState;
            CountdownValue = countdownValue;
            CurrentLap = currentLap;
            TotalLaps = totalLaps;
            CurrentPosition = currentPosition;
            ParticipantCount = participantCount;
            ElapsedRaceTime = elapsedRaceTime;
            RacingInputActive = racingInputActive;
        }

        public CountdownDisplayState CountdownState { get; }
        public float CountdownValue { get; }
        public int CurrentLap { get; }
        public int TotalLaps { get; }
        public int CurrentPosition { get; }
        public int ParticipantCount { get; }
        public float ElapsedRaceTime { get; }
        public bool RacingInputActive { get; }
    }
}
