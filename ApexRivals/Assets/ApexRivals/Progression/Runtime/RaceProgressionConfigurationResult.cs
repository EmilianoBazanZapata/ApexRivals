using System;

namespace ApexRivals.Progression.Runtime
{
    public readonly struct RaceProgressionConfigurationResult
    {
        public RaceProgressionConfigurationResult(RaceProgressionConfigurationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public RaceProgressionConfigurationStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == RaceProgressionConfigurationStatus.Valid;

        public static RaceProgressionConfigurationResult Valid()
        {
            return new RaceProgressionConfigurationResult(RaceProgressionConfigurationStatus.Valid, string.Empty);
        }

        public static RaceProgressionConfigurationResult Failure(RaceProgressionConfigurationStatus status, string message)
        {
            return new RaceProgressionConfigurationResult(status, message);
        }
    }
}
