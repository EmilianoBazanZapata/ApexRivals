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

        private void OnDestroy() => Uninstall();

        public void Install(ApplicationContext context)
        {
            Uninstall();
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
            _garagePresenter?.Dispose();
            if (garageView != null) garageView.Bind(null);
            _garagePresenter = null;
        }
    }
}
