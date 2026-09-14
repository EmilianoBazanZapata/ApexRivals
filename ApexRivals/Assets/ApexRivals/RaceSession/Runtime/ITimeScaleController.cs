namespace ApexRivals.RaceSession.Runtime
{
    public interface ITimeScaleController
    {
        float TimeScale { get; }

        void Pause();

        void Resume();
    }
}
