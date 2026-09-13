namespace ApexRivals.RaceSession.Runtime
{
    public interface IRaceSessionSetup
    {
        RaceSessionSetupResult Prepare();

        void Cleanup();
    }
}
