using System;

namespace ApexRivals.Garage.Runtime
{
    public sealed class SelectedVehicleState
    {
        public const string DefaultVehicleId = "starter";

        public SelectedVehicleState(string selectedVehicleId = DefaultVehicleId)
        {
            SetSelectedVehicleId(selectedVehicleId);
        }

        public event Action<string> Changed;

        public string SelectedVehicleId { get; private set; } = DefaultVehicleId;

        public void SetSelectedVehicleId(string selectedVehicleId)
        {
            var validVehicleId = string.IsNullOrWhiteSpace(selectedVehicleId)
                ? DefaultVehicleId
                : selectedVehicleId.Trim();

            if (SelectedVehicleId == validVehicleId)
            {
                return;
            }

            SelectedVehicleId = validVehicleId;
            Changed?.Invoke(SelectedVehicleId);
        }
    }
}
