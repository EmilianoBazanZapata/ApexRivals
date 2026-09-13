using System.Collections;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApexRivals.SceneFlow.Runtime
{
    public sealed class UnityContentSceneLoader : MonoBehaviour, IContentSceneLoader
    {
        private Scene _currentContentScene;

        public event Action<Scene> ContentSceneLoaded;
        public event Action<Scene> ContentSceneUnloading;

        public float Progress { get; private set; }
        public bool IsLoading { get; private set; }
        public string BootstrapSceneName => gameObject.scene.name;
        public string CurrentContentSceneName => _currentContentScene.IsValid() ? _currentContentScene.name : string.Empty;

        public Task<ContentSceneLoadResult> LoadContentSceneAsync(
            ContentSceneId sceneId,
            string sceneName,
            bool reloadCurrentScene,
            CancellationToken cancellationToken = default)
        {
            if (IsLoading)
            {
                return Task.FromResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.ConcurrentLoad, sceneId, sceneName, "A content scene is already loading."));
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return Task.FromResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.MissingSceneName, sceneId, sceneName, "The content scene name is empty."));
            }

            if (!reloadCurrentScene && CurrentContentSceneName == sceneName)
            {
                return Task.FromResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.DuplicateLoad, sceneId, sceneName, "The content scene is already loaded."));
            }

            var completion = new TaskCompletionSource<ContentSceneLoadResult>();
            StartCoroutine(LoadContentScene(sceneId, sceneName, reloadCurrentScene, completion, cancellationToken));
            return completion.Task;
        }

        private IEnumerator LoadContentScene(
            ContentSceneId sceneId,
            string sceneName,
            bool reloadCurrentScene,
            TaskCompletionSource<ContentSceneLoadResult> completion,
            CancellationToken cancellationToken)
        {
            IsLoading = true;
            Progress = 0f;
            var previousContentScene = _currentContentScene;

            var loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                IsLoading = false;
                completion.SetResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, sceneId, sceneName, "Unity did not create a load operation for the requested scene."));
                yield break;
            }

            while (!loadOperation.isDone)
            {
                if (FindNewestLoadedScene(sceneName, previousContentScene).isLoaded)
                {
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    IsLoading = false;
                    completion.SetResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, sceneId, sceneName, "The content scene load was cancelled."));
                    yield break;
                }

                Progress = Mathf.Clamp01(loadOperation.progress);
                yield return null;
            }

            var loadedScene = FindNewestLoadedScene(sceneName, previousContentScene);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                IsLoading = false;
                completion.SetResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, sceneId, sceneName, "The requested scene did not finish loading."));
                yield break;
            }

            SceneManager.SetActiveScene(loadedScene);

            var unloadedSceneName = string.Empty;
            if (previousContentScene.IsValid() && previousContentScene.isLoaded && previousContentScene.name != BootstrapSceneName)
            {
                ContentSceneUnloading?.Invoke(previousContentScene);
                var unloadOperation = SceneManager.UnloadSceneAsync(previousContentScene);
                if (unloadOperation != null)
                {
                    while (!unloadOperation.isDone)
                    {
                        Progress = Mathf.Clamp01(unloadOperation.progress);
                        yield return null;
                    }

                    unloadedSceneName = previousContentScene.name;
                }
            }

            _currentContentScene = loadedScene;
            ContentSceneLoaded?.Invoke(loadedScene);
            Progress = 1f;
            IsLoading = false;
            completion.SetResult(ContentSceneLoadResult.Success(sceneId, sceneName, unloadedSceneName));
        }

        private static Scene FindNewestLoadedScene(string sceneName, Scene previousContentScene)
        {
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName && scene.handle != previousContentScene.handle)
                {
                    return scene;
                }
            }

            return SceneManager.GetSceneByName(sceneName);
        }
    }
}
