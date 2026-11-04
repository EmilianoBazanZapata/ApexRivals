using System.Threading.Tasks;

namespace ApexRivals.SceneFlow.Runtime
{
    /// <summary>Visual cover (loading screen) shown around content scene loads; implemented by the UI module.</summary>
    public interface ISceneLoadingPresentation
    {
        bool IsVisible { get; }
        Task ShowAsync();
        void Hide(bool succeeded);
    }
}
