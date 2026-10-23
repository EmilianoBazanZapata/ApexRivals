namespace ApexRivals.Career.Runtime
{
    /// <summary>
    /// Identity of one race attempt, captured when the race scene installs: a unique ID, the circuit that was
    /// actually loaded, the car and whether it is the final. Results are attributed to this, never to whatever
    /// happens to be selected when the race ends.
    /// </summary>
    public sealed class CareerRaceAttempt
    {
        public CareerRaceAttempt(int attemptId, string trackId, string vehicleId, bool isFinale, bool counts, string reason)
        {
            AttemptId = attemptId;
            TrackId = trackId ?? string.Empty;
            VehicleId = vehicleId ?? string.Empty;
            IsFinale = isFinale;
            Counts = counts;
            Reason = reason ?? string.Empty;
        }

        public int AttemptId { get; }
        public string TrackId { get; }
        public string VehicleId { get; }
        public bool IsFinale { get; }

        /// <summary>False when the race cannot affect the career (unknown circuit, locked content).</summary>
        public bool Counts { get; }
        public string Reason { get; }
    }
}
