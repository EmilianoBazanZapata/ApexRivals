namespace ApexRivals.Bootstrap.Runtime
{
    public interface IContentSceneInstaller
    {
        void Install(ApplicationContext context);
        void Uninstall();
    }
}
