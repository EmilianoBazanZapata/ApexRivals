namespace ApexRivals.UI.Runtime
{
    /// <summary>Career status lines shared by the main menu and garage: progress, objective, collection, final.</summary>
    public readonly struct CareerPanelViewModel
    {
        public CareerPanelViewModel(
            int completionPercent,
            string completionText,
            string objectiveTitle,
            string objectiveDetail,
            string collectionText,
            string finaleText,
            string announcement,
            bool isComplete)
        {
            HasCareer = true;
            CompletionPercent = completionPercent;
            CompletionText = completionText ?? string.Empty;
            ObjectiveTitle = objectiveTitle ?? string.Empty;
            ObjectiveDetail = objectiveDetail ?? string.Empty;
            CollectionText = collectionText ?? string.Empty;
            FinaleText = finaleText ?? string.Empty;
            Announcement = announcement ?? string.Empty;
            IsComplete = isComplete;
        }

        /// <summary>False when no career is configured; views then hide the panel.</summary>
        public bool HasCareer { get; }
        public int CompletionPercent { get; }
        public string CompletionText { get; }
        public string ObjectiveTitle { get; }

        /// <summary>What to do next, with progress when the objective counts something.</summary>
        public string ObjectiveDetail { get; }
        public string CollectionText { get; }
        public string FinaleText { get; }

        /// <summary>Objectives completed outside a race since the screen was last shown.</summary>
        public string Announcement { get; }
        public bool IsComplete { get; }
    }
}
