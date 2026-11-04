using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ApexRivals.Settings.Runtime
{
    /// <summary>
    /// Applies the player's <see cref="GraphicsPreferences"/>: switches to the matching quality level (each level owns its
    /// URP asset) and sets post processing and anti-aliasing on every camera, again whenever a scene loads.
    /// </summary>
    public static class RuntimeGraphics
    {
        /// <summary>Quality level names in ProjectSettings for Low, Medium and High.</summary>
        private static readonly string[] QualityLevelNames = { "Low", "Medium", "PC" };

        private static bool _hooked;

        public static GraphicsPreferences Current { get; private set; } = GraphicsPreferences.Default;

        public static void Apply(GraphicsPreferences preferences)
        {
            Current = preferences;
            if (!_hooked)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                _hooked = true;
            }

            var level = System.Array.IndexOf(QualitySettings.names, QualityLevelNames[(int)preferences.Quality]);
            if (level >= 0 && level != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(level, true);
            }

            ApplyToCameras();
        }

        /// <summary>Post processing and anti-aliasing on every loaded camera.</summary>
        public static void ApplyToCameras()
        {
            foreach (var camera in Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Preview cameras rendering into textures (track thumbnails, captures) keep their own setup.
                if (camera.targetTexture != null) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderPostProcessing = Current.PostProcessing;
                data.antialiasing = Current.AntiAliasing switch
                {
                    AntiAliasingMode.Fxaa => AntialiasingMode.FastApproximateAntialiasing,
                    AntiAliasingMode.Smaa => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    _ => AntialiasingMode.None
                };
                data.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToCameras();
    }
}
