using System;

namespace ApexRivals.Garage.Runtime
{
    public sealed class SelectedVehicleState
    {
        public const string DefaultVehicleId = "starter";

        public SelectedVehicleState(string selectedVehicleId = "", bool isCommitted = false)
        {
            Restore(selectedVehicleId, isCommitted);
        }

        public event Action<string> Changed;

        public string SelectedVehicleId { get; private set; } = string.Empty;
        public bool HasCommittedVehicleSelection { get; private set; }

        public bool Commit(string selectedVehicleId)
        {
            if (string.IsNullOrWhiteSpace(selectedVehicleId))
            {
                return false;
            }

            var committedVehicleId = selectedVehicleId.Trim();
            if (HasCommittedVehicleSelection && SelectedVehicleId == committedVehicleId)
            {
                return false;
            }

            SelectedVehicleId = committedVehicleId;
            HasCommittedVehicleSelection = true;
            Changed?.Invoke(SelectedVehicleId);
            return true;
        }

        public void Restore(string selectedVehicleId, bool isCommitted)
        {
            var restoredVehicleId = isCommitted && !string.IsNullOrWhiteSpace(selectedVehicleId)
                ? selectedVehicleId.Trim()
                : string.Empty;
            var restoredCommitted = !string.IsNullOrWhiteSpace(restoredVehicleId);

            if (SelectedVehicleId == restoredVehicleId && HasCommittedVehicleSelection == restoredCommitted)
            {
                return;
            }

            SelectedVehicleId = restoredVehicleId;
            HasCommittedVehicleSelection = restoredCommitted;
            Changed?.Invoke(SelectedVehicleId);
        }

        public void ClearCommitment()
        {
            Restore(string.Empty, false);
        }
    }
}
