using System;
using System.Collections.Generic;
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

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                return Task.FromResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, sceneId, sceneName, "The requested scene is not available in the build settings."));
            }

            return LoadContentScene(sceneId, sceneName, cancellationToken);
        }

        private async Task<ContentSceneLoadResult> LoadContentScene(
            ContentSceneId sceneId,
            string sceneName,
            CancellationToken cancellationToken)
        {
            IsLoading = true;
            Progress = 0f;
            var previousContentScene = _currentContentScene;
            var disabledAudioListeners = new List<AudioListener>();
            var loadedScene = default(Scene);
            var committed = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                disabledAudioListeners = DisableAudioListeners(previousContentScene);
                var loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (loadOperation == null)
                {
                    throw new InvalidOperationException("Unity did not create a load operation for the requested scene.");
                }

                // Unity cannot cancel an in-flight load. Wait for it before honoring
                // cancellation, so the finally block can unload the candidate scene.
                while (!loadOperation.isDone)
                {
                    if (FindNewestLoadedScene(sceneName, previousContentScene).isLoaded)
                    {
                        break;
                    }

                    Progress = Mathf.Clamp01(loadOperation.progress);
                    await Task.Yield();
                }

                loadedScene = FindNewestLoadedScene(sceneName, previousContentScene);
                cancellationToken.ThrowIfCancellationRequested();
                if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                {
                    throw new InvalidOperationException("The requested scene did not finish loading.");
                }

                SceneManager.SetActiveScene(loadedScene);
                var unloadedSceneName = string.Empty;
                if (previousContentScene.IsValid() && previousContentScene.isLoaded && previousContentScene.name != BootstrapSceneName)
                {
                    unloadedSceneName = previousContentScene.name;
                    var unloadOperation = SceneManager.UnloadSceneAsync(previousContentScene);
                    if (unloadOperation == null)
                    {
                        throw new InvalidOperationException("Unity did not create an unload operation for the previous scene.");
                    }

                    // Only dispose the old session after Unity accepted the unload.
                    NotifySceneObservers(ContentSceneUnloading, previousContentScene);
                    while (!unloadOperation.isDone)
                    {
                        Progress = Mathf.Clamp01(unloadOperation.progress);
                        await Task.Yield();
                    }
                }

                _currentContentScene = loadedScene;
                committed = true;
                NotifySceneObservers(ContentSceneLoaded, loadedScene);
                Progress = 1f;
                return ContentSceneLoadResult.Success(sceneId, sceneName, unloadedSceneName);
            }
            catch (Exception exception)
            {
                return ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, sceneId, sceneName, exception.Message);
            }
            finally
            {
                try
                {
                    if (!committed && loadedScene.IsValid() && loadedScene.isLoaded && loadedScene.handle != previousContentScene.handle)
                    {
                        if (previousContentScene.IsValid() && previousContentScene.isLoaded)
                        {
                            SceneManager.SetActiveScene(previousContentScene);
                        }

                        var rollback = SceneManager.UnloadSceneAsync(loadedScene);
                        if (rollback != null)
                        {
                            while (!rollback.isDone)
                            {
                                await Task.Yield();
                            }
                        }
                    }
                }
                finally
                {
                    RestoreAudioListeners(disabledAudioListeners);
                    IsLoading = false;
                }
            }
        }

        private static void NotifySceneObservers(Action<Scene> observers, Scene scene)
        {
            if (observers == null)
            {
                return;
            }

            foreach (Action<Scene> observer in observers.GetInvocationList())
            {
                try
                {
                    observer(scene);
                }
                catch (Exception exception)
                {
                    // A broken subscriber must not strand the loading task or prevent
                    // other subscribers from releasing/installing their scene services.
                    Debug.LogException(exception);
                }
            }
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

            return default;
        }

        private static List<AudioListener> DisableAudioListeners(Scene scene)
        {
            var disabledListeners = new List<AudioListener>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return disabledListeners;
            }

            var listeners = new List<AudioListener>();
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                listeners.Clear();
                roots[index].GetComponentsInChildren(true, listeners);
                for (var listenerIndex = 0; listenerIndex < listeners.Count; listenerIndex++)
                {
                    var listener = listeners[listenerIndex];
                    if (listener != null && listener.enabled)
                    {
                        listener.enabled = false;
                        disabledListeners.Add(listener);
                    }
                }
            }

            return disabledListeners;
        }

        private static void RestoreAudioListeners(List<AudioListener> disabledListeners)
        {
            for (var index = 0; index < disabledListeners.Count; index++)
            {
                if (disabledListeners[index] != null)
                {
                    disabledListeners[index].enabled = true;
                }
            }
        }
    }
}
