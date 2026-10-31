using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Read-only career block (completion bar, objective, collection, final). Hidden without a career.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerPanelUguiView : MonoBehaviour
    {
        [SerializeField, Tooltip("Colors (and border widths) this control switches between with its state.")] private ApexUiPalette palette;
        private ApexUiPalette Palette => ApexUiPalette.Resolve(palette);

        [SerializeField] private GameObject root;
        [SerializeField] private Text completionText;
        [SerializeField, Tooltip("Horizontal filled image showing completion.")] private Image completionFill;
        [SerializeField, Tooltip("Chevron progress row; used instead of the filled image when assigned.")] private ChevronProgressBar completionBar;
        [SerializeField, Tooltip("\"68%\" beside a fixed CAREER PROGRESS title; without it the completion text carries the percent.")] private Text completionPercentText;
        [SerializeField] private Text objectiveTitleText;
        [SerializeField] private Text objectiveDetailText;
        [SerializeField] private Text collectionText;
        [SerializeField] private Text finaleText;
        [SerializeField] private Text announcementText;

        public void Render(CareerPanelViewModel viewModel)
        {
            var target = root != null ? root : gameObject;
            if (target.activeSelf != viewModel.HasCareer)
            {
                target.SetActive(viewModel.HasCareer);
            }

            if (!viewModel.HasCareer)
            {
                return;
            }

            if (completionPercentText != null)
            {
                UguiViewText.Set(completionPercentText, Mathf.Clamp(viewModel.CompletionPercent, 0, 100) + "%");
            }
            else
            {
                UguiViewText.Set(completionText, viewModel.CompletionText);
            }

            if (completionBar != null)
            {
                completionBar.Value = viewModel.CompletionPercent / 100f;
            }

            if (completionFill != null)
            {
                completionFill.fillAmount = Mathf.Clamp01(viewModel.CompletionPercent / 100f);
                completionFill.color = viewModel.IsComplete ? Palette.PrimaryText : Palette.PrimaryRed;
            }

            UguiViewText.Set(objectiveTitleText, viewModel.ObjectiveTitle);
            UguiViewText.Set(objectiveDetailText, viewModel.ObjectiveDetail);
            UguiViewText.Set(collectionText, viewModel.CollectionText);
            UguiViewText.Set(finaleText, viewModel.FinaleText);
            UguiViewText.Set(announcementText, viewModel.Announcement);
            if (announcementText != null && announcementText.gameObject.activeSelf != !string.IsNullOrEmpty(viewModel.Announcement))
            {
                announcementText.gameObject.SetActive(!string.IsNullOrEmpty(viewModel.Announcement));
            }
        }
    }
}
