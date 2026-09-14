using ApexRivals.RaceSetup.Runtime;

namespace ApexRivals.RaceSession.Runtime
{
    public readonly struct RaceSessionSetupResult
    {
        public RaceSessionSetupResult(bool succeeded, RaceSetupOutput output, string playerParticipantId, string message)
        {
            Succeeded = succeeded;
            Output = output;
            PlayerParticipantId = playerParticipantId;
            Message = message;
        }

        public bool Succeeded { get; }
        public RaceSetupOutput Output { get; }
        public string PlayerParticipantId { get; }
        public string Message { get; }

        public static RaceSessionSetupResult Success(RaceSetupOutput output, string playerParticipantId)
        {
            return new RaceSessionSetupResult(true, output, playerParticipantId, string.Empty);
        }

        public static RaceSessionSetupResult Failure(string message)
        {
            return new RaceSessionSetupResult(false, null, string.Empty, message);
        }
    }
}
