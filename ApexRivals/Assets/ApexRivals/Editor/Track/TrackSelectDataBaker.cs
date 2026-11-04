using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ApexRivals.AI.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Tracks.Configuration;
using ApexRivals.Tracks.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ApexRivals.Editor.Track
{
    /// <summary>
    /// Fills every <see cref="TrackDefinition"/> with data read from its race scene: the minimap outline
    /// (the AI racing line, rotated so it starts at the grid), the lap length and a preview image rendered
    /// from behind the pole position. Only missing fields are initialized; authored data and existing image import settings are preserved.
    /// </summary>
    public static class TrackSelectDataBaker
    {
        private const int PreviewWidth = 1280;
        private const int PreviewHeight = 600;
        private const int MinimapPointBudget = 160;

        [MenuItem("ApexRivals/Track/Bake Track Select Data")]
        public static void BakeFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Debug.Log(BakeAll());
        }

        public static string BakeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before baking track data.");
            var report = new StringBuilder();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(TrackDefinition)))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var definition = AssetDatabase.LoadAssetAtPath<TrackDefinition>(path);
                    if (definition != null) report.AppendLine(Bake(definition, path));
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return report.ToString();
        }

        private static string Bake(TrackDefinition definition, string definitionPath)
        {
            var trackId = definition.TrackId;
            var authored = new SerializedObject(definition);
            var needsLength = authored.FindProperty("lengthMeters").floatValue <= 0f;
            var needsMinimap = authored.FindProperty("minimapPoints").arraySize == 0;
            var needsPreview = authored.FindProperty("preview").objectReferenceValue == null;
            if (!needsLength && !needsMinimap && !needsPreview) return trackId + ": existing track data preserved.";
            var scenePath = FindScenePath(definition.SceneName);
            if (scenePath == null)
            {
                return $"{trackId}: scene '{definition.SceneName}' is not in the build settings.";
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var racingLine = Object.FindFirstObjectByType<RacingLine>(FindObjectsInactive.Include);
            var grid = Object.FindFirstObjectByType<StartingGrid>(FindObjectsInactive.Include);
            if (racingLine == null || racingLine.WaypointCount < 3)
            {
                return $"{trackId}: no racing line in {scenePath}.";
            }

            var pole = grid != null && grid.TryGetSpawnPose(0, out var pose)
                ? pose
                : new SpawnPose(racingLine.GetWaypointPosition(0), Quaternion.identity);
            var startIndex = racingLine.GetNearestWaypointIndex(pole.Position);
            var line = new List<Vector2>(racingLine.WaypointCount);
            for (var index = 0; index < racingLine.WaypointCount; index++)
            {
                var position = racingLine.GetWaypointPosition(index);
                line.Add(new Vector2(position.x, position.z));
            }

            var length = TrackMinimap.ClosedLength(line);
            var minimap = TrackMinimap.Normalize(Downsample(line, MinimapPointBudget, ref startIndex), startIndex);
            var preview = needsPreview ? CapturePreview(definitionPath, pole) : null;

            // Opening the scene and importing the preview unload unreferenced assets, so reload the definition.
            definition = AssetDatabase.LoadAssetAtPath<TrackDefinition>(definitionPath);
            var serialized = new SerializedObject(definition);
            if (needsLength) serialized.FindProperty("lengthMeters").floatValue = Mathf.Round(length);
            var points = serialized.FindProperty("minimapPoints");
            if (needsMinimap)
            {
                points.arraySize = minimap.Length;
                for (var index = 0; index < minimap.Length; index++)
                    points.GetArrayElementAtIndex(index).vector2Value = minimap[index];
            }

            if (preview != null)
            {
                serialized.FindProperty("preview").objectReferenceValue = preview;
            }

            if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(definition);
            return $"{trackId}: missing fields initialized; existing length, minimap and preview preserved.";
        }

        private static List<Vector2> Downsample(List<Vector2> line, int budget, ref int startIndex)
        {
            if (line.Count <= budget)
            {
                return line;
            }

            var step = Mathf.CeilToInt(line.Count / (float)budget);
            var sampled = new List<Vector2>(budget);
            for (var index = startIndex % step; index < line.Count; index += step)
            {
                sampled.Add(line[index]);
            }

            startIndex /= step;
            return sampled;
        }

        private static Sprite CapturePreview(string definitionPath, SpawnPose pole)
        {
            var pngPath = Path.Combine(Path.GetDirectoryName(definitionPath) ?? "Assets", Path.GetFileNameWithoutExtension(definitionPath) + "_Preview.png").Replace('\\', '/');
            if (File.Exists(pngPath)) return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            var forward = pole.Rotation * Vector3.forward;
            var cameraObject = new GameObject("TrackPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var texture = new RenderTexture(PreviewWidth, PreviewHeight, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(PreviewWidth, PreviewHeight, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.fieldOfView = 55f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                // Raised three-quarter view from behind the grid, looking down the opening straight.
                var right = pole.Rotation * Vector3.right;
                camera.transform.position = pole.Position - forward * 22f + right * 6f + Vector3.up * 10f;
                camera.transform.LookAt(pole.Position + forward * 45f);
                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, PreviewWidth, PreviewHeight), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;

                using (var file = new FileStream(pngPath, FileMode.CreateNew, FileAccess.Write))
                {
                    var bytes = readback.EncodeToPNG();
                    file.Write(bytes, 0, bytes.Length);
                }
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(pngPath) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = false;
                    importer.SaveAndReimport();
                }

                return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(readback);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        private static string FindScenePath(string sceneName)
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                {
                    return scene.path;
                }
            }

            return null;
        }
    }
}
