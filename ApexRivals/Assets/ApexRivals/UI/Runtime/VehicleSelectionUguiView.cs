using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VehicleSelectionUguiView : MonoBehaviour, IVehicleSelectionView, ICancelHandler
    {
        [SerializeField] private Text vehicleNameText;
        [SerializeField] private Text statsText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text statusText;

        private VehicleSelectionPresenter _presenter;
        private VehicleSelectionViewModel _current;
        private bool _subscribed;

        public void Bind(VehicleSelectionPresenter presenter)
        {
            _presenter = presenter;
            Subscribe();
        }

        public void Render(VehicleSelectionViewModel viewModel)
        {
            _current = viewModel;
            if (viewModel.SelectedIndex >= 0 && viewModel.SelectedIndex < viewModel.Vehicles.Count)
            {
                var selected = viewModel.Vehicles[viewModel.SelectedIndex];
                UguiViewText.Set(vehicleNameText, selected.DisplayName);
                UguiViewText.Set(statsText, UguiViewText.FormatStats(selected.EffectiveStats));
            }
            else
            {
                UguiViewText.Set(vehicleNameText, "No Vehicle");
                UguiViewText.Set(statsText, string.Empty);
            }

            SetButton(previousButton, viewModel.CanSelectPrevious);
            SetButton(nextButton, viewModel.CanSelectNext);
            SetButton(confirmButton, viewModel.CanConfirm || viewModel.CanContinue);
            UguiViewText.Set(confirmButton != null ? confirmButton.GetComponentInChildren<Text>() : null, viewModel.CanContinue ? "Continue" : "Confirm");
            UguiViewText.Set(statusText, viewModel.MessageKey);
        }

        private void OnEnable()
        {
            Subscribe();
            _presenter?.Present();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (previousButton != null)
            {
                previousButton.onClick.AddListener(OnPreviousClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(OnNextClicked);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(OnPreviousClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(OnNextClicked);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            _subscribed = false;
        }

        private void OnPreviousClicked()
        {
            _presenter?.SelectPrevious();
        }

        private void OnNextClicked()
        {
            _presenter?.SelectNext();
        }

        private async void OnConfirmClicked()
        {
            if (_presenter == null)
            {
                return;
            }

            if (_current.CanContinue)
            {
                await Run(_presenter.Continue());
                return;
            }

            _presenter.ConfirmSelection();
        }

        public void OnCancel(BaseEventData eventData)
        {
            eventData?.Use();
            _presenter?.Cancel();
        }

        private static async Task Run(Task task)
        {
            await task;
        }

        private static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
