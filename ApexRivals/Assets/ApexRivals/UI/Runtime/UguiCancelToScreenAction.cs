using UnityEngine;
using UnityEngine.EventSystems;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class UguiCancelToScreenAction : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private UguiScreenNavigator screenNavigator;
        [SerializeField] private PresentationScreenState sourceScreen = PresentationScreenState.Settings;
        [SerializeField] private PresentationScreenState targetScreen = PresentationScreenState.Main;

        public void OnCancel(BaseEventData eventData)
        {
            if (screenNavigator == null || screenNavigator.CurrentScreen != sourceScreen)
            {
                return;
            }

            eventData?.Use();
            screenNavigator.Show(targetScreen);
        }
    }
}
