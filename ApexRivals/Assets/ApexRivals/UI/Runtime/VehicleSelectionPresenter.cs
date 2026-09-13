using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class VehicleSelectionPresenter : IInitialVehicleSelectionProgress
    {
        private readonly IVehicleSelectionView _view;
        private readonly VehicleSelectionService _selectionService;
        private readonly SceneFlowService _sceneFlow;
        private readonly IPresentationScreenNavigator _screenNavigator;
        private bool _saving;
        private PresentationFailure _failure;
        private string _messageKey = string.Empty;

        public VehicleSelectionPresenter(IVehicleSelectionView view, VehicleSelectionService selectionService, SceneFlowService sceneFlow, IPresentationScreenNavigator screenNavigator = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            _screenNavigator = screenNavigator;
        }

        public bool HasCommittedVehicleSelection => _selectionService.HasValidCommittedVehicleSelection;
        public VehicleSelectionViewModel Current { get; private set; }

        public void Present()
        {
            Current = CreateModel();
            _view.Render(Current);
        }

        public void SelectPrevious()
        {
            if (_saving || Current.Vehicles == null || Current.Vehicles.Count == 0 || Current.SelectedIndex <= 0)
            {
                return;
            }

            Select(Current.Vehicles[Current.SelectedIndex - 1].VehicleId);
        }

        public void SelectNext()
        {
            if (_saving || Current.Vehicles == null || Current.Vehicles.Count == 0 || Current.SelectedIndex >= Current.Vehicles.Count - 1)
            {
                return;
            }

            Select(Current.Vehicles[Current.SelectedIndex + 1].VehicleId);
        }

        public void Select(string vehicleId)
        {
            if (_saving)
            {
                return;
            }

            var result = _selectionService.Select(vehicleId);
            if (!result.Succeeded)
            {
                _failure = result.Status == VehicleSelectionStatus.SaveFailed
                    ? PresentationFailure.SaveFailed
                    : PresentationFailure.InvalidVehicleSelection;
                _messageKey = result.Status == VehicleSelectionStatus.SaveFailed
                    ? "ui.vehicleSelection.saveFailed"
                    : "ui.vehicleSelection.invalidSelection";
            }
            else
            {
                _failure = PresentationFailure.None;
                _messageKey = string.Empty;
            }

            Present();
        }

        public void ConfirmSelection()
        {
            if (_saving || string.IsNullOrWhiteSpace(_selectionService.CurrentCandidateVehicleId))
            {
                _failure = PresentationFailure.InvalidVehicleSelection;
                _messageKey = "ui.vehicleSelection.invalidSelection";
                Present();
                return;
            }

            var result = _selectionService.ConfirmSelection();
            if (result.Succeeded)
            {
                _failure = PresentationFailure.None;
                _messageKey = "ui.vehicleSelection.confirmed";
            }
            else
            {
                _failure = result.Status == VehicleSelectionStatus.SaveFailed
                    ? PresentationFailure.SaveFailed
                    : PresentationFailure.InvalidVehicleSelection;
                _messageKey = result.Status == VehicleSelectionStatus.SaveFailed
                    ? "ui.vehicleSelection.saveFailed"
                    : "ui.vehicleSelection.invalidSelection";
            }

            Present();
        }

        public async Task Continue()
        {
            if (_saving || !_selectionService.HasValidCommittedVehicleSelection)
            {
                return;
            }

            _saving = true;
            Present();
            var result = await _sceneFlow.StartRace();
            _saving = false;

            if (!result.Succeeded)
            {
                _failure = PresentationFailure.NavigationFailed;
                _messageKey = "ui.vehicleSelection.continueFailed";
            }

            Present();
        }

        public void Cancel()
        {
            if (_saving)
            {
                return;
            }

            _selectionService.CancelPreview();
            _failure = PresentationFailure.None;
            _messageKey = string.Empty;
            _screenNavigator?.Show(PresentationScreenState.Main);
        }

        private VehicleSelectionViewModel CreateModel()
        {
            var state = _selectionService.GetState();
            var vehicles = new List<VehicleSelectionItemViewModel>(state.Vehicles.Count);
            var selectedIndex = -1;

            for (var index = 0; index < state.Vehicles.Count; index++)
            {
                var item = state.Vehicles[index];
                if (!item.Selectable)
                {
                    continue;
                }

                vehicles.Add(new VehicleSelectionItemViewModel(
                    item.VehicleId,
                    item.DisplayName,
                    item.BaseStats,
                    item.EffectivePlayerStats,
                    item.Selected,
                    item.Selectable));

                if (item.Selected)
                {
                    selectedIndex = vehicles.Count - 1;
                }
            }

            return new VehicleSelectionViewModel(
                vehicles,
                selectedIndex,
                state.SelectedVehicleId,
                selectedIndex > 0 && !_saving,
                selectedIndex >= 0 && selectedIndex < vehicles.Count - 1 && !_saving,
                selectedIndex >= 0 && !_saving,
                _selectionService.HasValidCommittedVehicleSelection && !_saving,
                state.HasCommittedVehicleSelection,
                _saving,
                _failure,
                _messageKey);
        }
    }
}
