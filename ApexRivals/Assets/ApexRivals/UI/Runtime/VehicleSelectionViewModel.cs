using System.Collections.Generic;

namespace ApexRivals.UI.Runtime
{
    public readonly struct VehicleSelectionViewModel
    {
        public VehicleSelectionViewModel(
            IReadOnlyList<VehicleSelectionItemViewModel> vehicles,
            int selectedIndex,
            string selectedVehicleId,
            bool canSelectPrevious,
            bool canSelectNext,
            bool canConfirm,
            bool canContinue,
            bool hasCommittedVehicleSelection,
            bool isSaving,
            PresentationFailure failure,
            string messageKey)
        {
            Vehicles = vehicles;
            SelectedIndex = selectedIndex;
            SelectedVehicleId = selectedVehicleId;
            CanSelectPrevious = canSelectPrevious;
            CanSelectNext = canSelectNext;
            CanConfirm = canConfirm;
            CanContinue = canContinue;
            HasCommittedVehicleSelection = hasCommittedVehicleSelection;
            IsSaving = isSaving;
            Failure = failure;
            MessageKey = messageKey;
        }

        public IReadOnlyList<VehicleSelectionItemViewModel> Vehicles { get; }
        public int SelectedIndex { get; }
        public string SelectedVehicleId { get; }
        public bool CanSelectPrevious { get; }
        public bool CanSelectNext { get; }
        public bool CanConfirm { get; }
        public bool CanContinue { get; }
        public bool HasCommittedVehicleSelection { get; }
        public bool IsSaving { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }
}
