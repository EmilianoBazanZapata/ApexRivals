namespace ApexRivals.Composition.Runtime
{
    public interface IContentSceneInstaller
    {
        void Install(ApplicationContext context);
        void Uninstall();
    }
}
