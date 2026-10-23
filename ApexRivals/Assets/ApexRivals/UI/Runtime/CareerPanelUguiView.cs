using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Read-only career block (completion bar, objective, collection, final). Hidden without a career.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerPanelUguiView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text completionText;
        [SerializeField, Tooltip("Horizontal filled image showing completion.")] private Image completionFill;
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

            UguiViewText.Set(completionText, viewModel.CompletionText);
            if (completionFill != null)
            {
                completionFill.fillAmount = Mathf.Clamp01(viewModel.CompletionPercent / 100f);
                completionFill.color = viewModel.IsComplete ? ApexUiVisualStyle.PrimaryText : ApexUiVisualStyle.PrimaryRed;
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
