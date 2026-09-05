using System.Threading;
using System.Threading.Tasks;

namespace ApexRivals.SceneFlow.Runtime
{
    public interface IContentSceneLoader
    {
        float Progress { get; }
        bool IsLoading { get; }
        string BootstrapSceneName { get; }
        string CurrentContentSceneName { get; }

        Task<ContentSceneLoadResult> LoadContentSceneAsync(
            ContentSceneId sceneId,
            string sceneName,
            bool reloadCurrentScene,
            CancellationToken cancellationToken = default);
    }
}
