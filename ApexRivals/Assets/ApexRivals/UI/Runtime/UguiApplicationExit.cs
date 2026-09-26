using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ApexRivals.UI.Runtime
{
    public sealed class UguiApplicationExit : IApplicationExit
    {
        public void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
