namespace ApexRivals.UI.Runtime
{
    /// <summary>Career part of the garage: the previewed car's ownership and price, the buy action and feedback.</summary>
    public readonly struct GarageCareerViewModel
    {
        public GarageCareerViewModel(
            CareerPanelViewModel panel,
            string candidateVehicleName,
            bool candidateOwned,
            string candidateStatus,
            bool canBuyCandidate,
            string feedback)
        {
            Panel = panel;
            CandidateVehicleName = candidateVehicleName ?? string.Empty;
            CandidateOwned = candidateOwned;
            CandidateStatus = candidateStatus ?? string.Empty;
            CanBuyCandidate = canBuyCandidate;
            Feedback = feedback ?? string.Empty;
        }

        public CareerPanelViewModel Panel { get; }
        public bool HasCareer => Panel.HasCareer;
        public string CandidateVehicleName { get; }
        public bool CandidateOwned { get; }

        /// <summary>"OWNED", or the price and whatever still blocks buying the car.</summary>
        public string CandidateStatus { get; }
        public bool CanBuyCandidate { get; }

        /// <summary>Purchase or objective message shown instead of the generic purchase banner.</summary>
        public string Feedback { get; }
    }
}
