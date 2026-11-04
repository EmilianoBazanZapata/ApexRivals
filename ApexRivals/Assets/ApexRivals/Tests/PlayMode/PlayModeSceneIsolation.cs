using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApexRivals.Tests.PlayMode
{
    /// <summary>
    /// Destroys every root GameObject a test created, so navigators and EventSystems
    /// from one test cannot keep running and steal selection in the next one.
    /// </summary>
    internal sealed class PlayModeSceneIsolation
    {
        private readonly HashSet<GameObject> _preexistingRoots = new HashSet<GameObject>();

        public void Capture()
        {
            _preexistingRoots.Clear();
            _preexistingRoots.UnionWith(SceneManager.GetActiveScene().GetRootGameObjects());
        }

        public void Cleanup()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                if (roots[index] != null && !_preexistingRoots.Contains(roots[index]))
                {
                    Object.DestroyImmediate(roots[index]);
                }
            }

            _preexistingRoots.Clear();
            ApexRivals.UI.Runtime.UiInputModality.Set(ApexRivals.UI.Runtime.UiInputMode.Navigation);
        }
    }
}
