namespace ApexRivals.Career.Runtime
{
    public readonly struct CareerRaceRequestResult
    {
        public CareerRaceRequestResult(CareerRaceRequestStatus status, string trackId, string sceneName, bool isFinale, string message)
        {
            Status = status;
            TrackId = trackId ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            IsFinale = isFinale;
            Message = message ?? string.Empty;
        }

        public CareerRaceRequestStatus Status { get; }
        public string TrackId { get; }

        /// <summary>Content scene to load for the race; set when the request is accepted.</summary>
        public string SceneName { get; }
        public bool IsFinale { get; }
        public string Message { get; }
        public bool Succeeded => Status == CareerRaceRequestStatus.Ready;
    }
}
