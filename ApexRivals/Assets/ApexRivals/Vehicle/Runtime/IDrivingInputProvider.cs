namespace ApexRivals.Vehicle.Runtime
{
    public interface IDrivingInputProvider
    {
        DrivingInput CurrentInput { get; }

        void ConsumeResetRequest();

        void ConsumeRecoveryRequest();
    }
}
