using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Tracks.Configuration;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageUiSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private UguiScreenNavigator screenNavigator;
        [SerializeField] private GarageUguiView garageView;
        [SerializeField, Tooltip("Optional. Without it (or the catalog) Start Race loads the configured race scene directly.")] private TrackSelectionUguiView trackSelectionView;
        [SerializeField] private TrackCatalogDefinition trackCatalog;

        private GaragePresenter _garagePresenter;
        private TrackSelectionPresenter _trackSelectionPresenter;

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

            if (trackSelectionView != null && trackCatalog != null)
            {
                _trackSelectionPresenter = new TrackSelectionPresenter(trackSelectionView, trackCatalog.CreateCatalog(), context.Navigation, screenNavigator, context.Career);
                trackSelectionView.Bind(_trackSelectionPresenter);
            }

            _garagePresenter = new GaragePresenter(garageView, context.GarageService, context.VehicleSelectionService, context.Navigation, _trackSelectionPresenter, context.Career);
            garageView.Bind(_garagePresenter);
            screenNavigator.Show(PresentationScreenState.Garage);
            _garagePresenter.Present();
            if (context.Navigation.ConsumeTrackSelectionRequest() && _trackSelectionPresenter != null)
            {
                _trackSelectionPresenter.Open();
            }
        }

        public void Uninstall()
        {
            _garagePresenter?.Dispose();
            _trackSelectionPresenter?.Dispose();
            if (garageView != null) garageView.Bind(null);
            if (trackSelectionView != null) trackSelectionView.Bind(null);
            _garagePresenter = null;
            _trackSelectionPresenter = null;
        }
    }
}
