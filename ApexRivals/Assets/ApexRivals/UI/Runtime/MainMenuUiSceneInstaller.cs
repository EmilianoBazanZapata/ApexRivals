using ApexRivals.Bootstrap.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MainMenuUiSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private UguiScreenNavigator screenNavigator;
        [SerializeField] private MainMenuUguiView mainMenuView;
        [SerializeField] private VehicleSelectionUguiView vehicleSelectionView;
        [SerializeField] private SettingsUguiView settingsView;

        private MainMenuPresenter _mainMenuPresenter;
        private VehicleSelectionPresenter _vehicleSelectionPresenter;
        private SettingsPresenter _settingsPresenter;

        public void Install(ApplicationContext context)
        {
            if (context == null || context.Navigation == null || context.VehicleSelectionService == null || context.SettingsService == null)
            {
                Debug.LogError("Main menu UI could not install because required application services are missing.", this);
                return;
            }

            if (screenNavigator == null || mainMenuView == null || vehicleSelectionView == null || settingsView == null)
            {
                Debug.LogError("Main menu UI has missing serialized references.", this);
                return;
            }

            _vehicleSelectionPresenter = new VehicleSelectionPresenter(vehicleSelectionView, context.VehicleSelectionService, context.Navigation);
            _settingsPresenter = new SettingsPresenter(settingsView, context.SettingsService);
            _mainMenuPresenter = new MainMenuPresenter(mainMenuView, context.Navigation, _vehicleSelectionPresenter, screenNavigator, new UguiApplicationExit());

            vehicleSelectionView.Bind(_vehicleSelectionPresenter);
            settingsView.Bind(_settingsPresenter, screenNavigator, PresentationScreenState.Main);
            mainMenuView.Bind(_mainMenuPresenter);

            screenNavigator.Show(PresentationScreenState.Main);
            _vehicleSelectionPresenter.Present();
            _settingsPresenter.Present();
            _mainMenuPresenter.Present();
        }

        public void Uninstall()
        {
            _mainMenuPresenter = null;
            _vehicleSelectionPresenter = null;
            _settingsPresenter = null;
        }
    }
}
