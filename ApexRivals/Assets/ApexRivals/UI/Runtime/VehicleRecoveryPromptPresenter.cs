using System;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public sealed class VehicleRecoveryPromptPresenter : IDisposable
    {
        private readonly IVehicleRecoveryPromptView _view;
        private readonly VehicleRoofRecoveryDetector _detector;
        private bool _disposed;

        public VehicleRecoveryPromptPresenter(
            IVehicleRecoveryPromptView view,
            VehicleRoofRecoveryDetector detector)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _detector.RecoveryAvailabilityChanged += OnRecoveryAvailabilityChanged;
            _detector.RecoveryRequested += OnRecoveryRequested;
            _detector.RecoveryCompleted += OnRecoveryCompleted;
        }

        public void Present()
        {
            var isAvailable = _detector.CanRecoverVehicle;
            _view.Render(isAvailable);
            if (isAvailable)
            {
                Debug.Log("[Vehicle Recovery] Manual recovery available.", _detector);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _detector.RecoveryAvailabilityChanged -= OnRecoveryAvailabilityChanged;
            _detector.RecoveryRequested -= OnRecoveryRequested;
            _detector.RecoveryCompleted -= OnRecoveryCompleted;
            _view.Render(false);
            _disposed = true;
        }

        private void OnRecoveryAvailabilityChanged(bool isAvailable)
        {
            _view.Render(isAvailable);
            Debug.Log(
                isAvailable
                    ? "[Vehicle Recovery] Manual recovery available."
                    : "[Vehicle Recovery] Recovery prompt hidden.",
                _detector);
        }

        private void OnRecoveryRequested(RecoveryInputSource inputSource)
        {
            var inputLabel = inputSource == RecoveryInputSource.GamepadView ? "View" : "E";
            Debug.Log($"[Vehicle Recovery] Player requested recovery with {inputLabel}.", _detector);
        }

        private void OnRecoveryCompleted(RecoveryInputSource inputSource)
        {
            Debug.Log("[Vehicle Recovery] Recovery completed.", _detector);
        }
    }
}
