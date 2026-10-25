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
            // Initial boot and race retries deliberately keep their existing presentation.
            var covered = !reloadCurrentScene && !string.IsNullOrEmpty(CurrentContentSceneName)
                && (sceneId == ContentSceneId.Race || sceneId == ContentSceneId.MainMenu);
            if (!covered)
                return await _inner.LoadContentSceneAsync(sceneId, sceneName, reloadCurrentScene, cancellationToken);

            var succeeded = false;
            try
            {
                try { await _presentation.ShowAsync(); }
                catch (Exception exception) { Report(exception); }
                var result = await _inner.LoadContentSceneAsync(sceneId, sceneName, reloadCurrentScene, cancellationToken);
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

        private void Report(Exception exception)
        {
            try { _report?.Invoke(exception); }
            catch { /* Diagnostics cannot strand navigation. */ }
        }
    }
}
