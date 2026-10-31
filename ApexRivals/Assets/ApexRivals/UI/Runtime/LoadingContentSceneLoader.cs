using System;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.UI.Runtime
{
    public interface ISceneLoadingPresentation
    {
        bool IsVisible { get; }
        Task ShowAsync();
        void Hide(bool succeeded);
    }

    /// <summary>Presentation around accepted content loads; scene-flow validation remains the owner of navigation.</summary>
    public sealed class LoadingContentSceneLoader : IContentSceneLoader
    {
        private readonly IContentSceneLoader _inner;
        private readonly ISceneLoadingPresentation _presentation;
        private readonly Action<Exception> _report;
        private ContentSceneId? _currentScene;

        public LoadingContentSceneLoader(IContentSceneLoader inner, ISceneLoadingPresentation presentation, Action<Exception> report = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _report = report;
        }

        public float Progress => _inner.Progress;
        public bool IsLoading => _inner.IsLoading;
        public string BootstrapSceneName => _inner.BootstrapSceneName;
        public string CurrentContentSceneName => _inner.CurrentContentSceneName;

        public async Task<ContentSceneLoadResult> LoadContentSceneAsync(ContentSceneId sceneId, string sceneName,
            bool reloadCurrentScene, CancellationToken cancellationToken = default)
        {
            // Initial boot and race retries deliberately keep their existing presentation; the light garage-to-menu
            // hop switches directly, without the loading screen.
            var covered = !reloadCurrentScene && !string.IsNullOrEmpty(CurrentContentSceneName)
                && (sceneId == ContentSceneId.Race
                    || (sceneId == ContentSceneId.MainMenu && _currentScene != ContentSceneId.Garage));
            if (!covered)
                return Track(await _inner.LoadContentSceneAsync(sceneId, sceneName, reloadCurrentScene, cancellationToken));

            var succeeded = false;
            try
            {
                try { await _presentation.ShowAsync(); }
                catch (Exception exception) { Report(exception); }
                var result = Track(await _inner.LoadContentSceneAsync(sceneId, sceneName, reloadCurrentScene, cancellationToken));
                succeeded = result.Succeeded;
                return result;
            }
            finally
            {
                // The inner loader completes only after installation or rollback, including cancellation.
                try { _presentation.Hide(succeeded); }
                catch (Exception exception) { Report(exception); }
            }
        }

        private ContentSceneLoadResult Track(ContentSceneLoadResult result)
        {
            if (result.Succeeded) _currentScene = result.SceneId;
            return result;
        }

        private void Report(Exception exception)
        {
            try { _report?.Invoke(exception); }
            catch { /* Diagnostics cannot strand navigation. */ }
        }
    }
}
