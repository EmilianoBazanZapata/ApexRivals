using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceHudUguiView : MonoBehaviour, IRaceHudView
    {
        [SerializeField] private Text countdownText;
        [SerializeField] private Text lapText;
        [SerializeField] private Text positionText;
        [SerializeField] private Text timeText;

        private RaceHudPresenter _presenter;

        public void Bind(RaceHudPresenter presenter)
        {
            _presenter = presenter;
        }

        public void Render(RaceHudViewModel viewModel)
        {
            UguiViewText.Set(countdownText, viewModel.CountdownState == CountdownDisplayState.Hidden ? string.Empty : Mathf.CeilToInt(viewModel.CountdownValue).ToString());
            UguiViewText.Set(lapText, $"Lap {viewModel.CurrentLap}/{viewModel.TotalLaps}");
            UguiViewText.Set(positionText, $"Position {viewModel.CurrentPosition}/{viewModel.ParticipantCount}");
            UguiViewText.Set(timeText, UguiViewText.FormatTime(viewModel.ElapsedRaceTime));
        }

        private void Update()
        {
            _presenter?.UpdateElapsedTimeSnapshot();
        }
    }
}
