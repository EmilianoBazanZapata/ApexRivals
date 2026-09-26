using ApexRivals.Bootstrap.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageUiSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private UguiScreenNavigator screenNavigator;
        [SerializeField] private GarageUguiView garageView;

        private GaragePresenter _garagePresenter;

        public void Install(ApplicationContext context)
        {
            if (context == null || context.GarageService == null || context.VehicleSelectionService == null || context.Navigation == null)
            {
                Debug.LogError("Garage UI could not install because required application services are missing.", this);
                return;
            }

            if (screenNavigator == null || garageView == null)
            {
                Debug.LogError("Garage UI has missing serialized references.", this);
                return;
            }

            _garagePresenter = new GaragePresenter(garageView, context.GarageService, context.VehicleSelectionService, context.Navigation);
            garageView.Bind(_garagePresenter);
            screenNavigator.Show(PresentationScreenState.Garage);
            _garagePresenter.Present();
        }

        public void Uninstall()
        {
            _garagePresenter = null;
        }
    }
}
