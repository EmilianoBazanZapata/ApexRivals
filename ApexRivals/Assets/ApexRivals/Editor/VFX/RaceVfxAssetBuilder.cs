using System;
using System.Collections.Generic;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Camera.Runtime;
using ApexRivals.VFX.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ApexRivals.Editor.VFX
{
    /// <summary>Creates the authored VFX assets and binds the three existing circuits. Explicit, safe to rerun.</summary>
    public static class RaceVfxAssetBuilder
    {
        private const string Root = "Assets/ApexRivals/Art/VFX/";
        private const string DefinitionPath = "Assets/ApexRivals/Content/Configuration/VFX/RaceVfxDefinition.asset";

        [MenuItem("Apex Rivals/VFX/Create and Bind Low Poly Race Effects")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before authoring VFX.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits before binding VFX.");
            Folder(Root + "Materials");
            Folder(Root + "Meshes");
            Folder(Root + "Prefabs");
            Folder("Assets/ApexRivals/Content/Configuration/VFX");
            var smoke = Material("DriftSmoke", "ApexRivals/VFX/Faceted Particle", m => m.SetFloat("_LightInfluence", 0.85f));
            var sparks = Material("ImpactSparks", "ApexRivals/VFX/Angular Streak", m => { m.SetFloat("_Glow", 2.5f); m.SetFloat("_DstBlend", (float)BlendMode.One); });
            var rain = Material("Rain", "ApexRivals/VFX/Angular Streak", m => { m.SetFloat("_Glow", 1f); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); });
            var confetti = Material("VictoryConfetti", "ApexRivals/VFX/Confetti Overlay");
            var tireMarks = Material("TireMarks", "ApexRivals/VFX/Tire Marks");
            var mesh = CreateSmokeMesh();
            var definition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                if (System.IO.File.Exists(DefinitionPath)) throw new InvalidOperationException("Existing definition cannot be loaded: " + DefinitionPath);
                definition = ScriptableObject.CreateInstance<RaceVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            var refs = new SerializedObject(definition);
            FillReference(refs, "driftSmokePrefab", Prefab("DriftSmoke", smoke, mesh, 0));
            FillReference(refs, "impactSparksPrefab", Prefab("ImpactSparks", sparks, null, 1));
            FillReference(refs, "rainPrefab", Prefab("Rain", rain, null, 2));
            FillReference(refs, "confettiMaterial", confetti);
            FillReference(refs, "skidmarkMaterial", tireMarks);
            if (refs.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(definition);

            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var name in new[] { "Race", "RaceDunes", "RaceSuzuka", "RaceRiverdale" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/ApexRivals/Scenes/" + name + ".unity", OpenSceneMode.Single);
                    RaceSessionSceneInstaller session = null;
                    WheelVehicleFollowCamera follow = null;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (session == null) session = root.GetComponentInChildren<RaceSessionSceneInstaller>(true);
                        if (follow == null) follow = root.GetComponentInChildren<WheelVehicleFollowCamera>(true);
                    }
                    if (session == null || follow == null) throw new InvalidOperationException("Missing race composition in " + name);
                    var installer = session.GetComponent<RaceVfxSceneInstaller>();
                    var created = installer == null;
                    if (created) installer = Undo.AddComponent<RaceVfxSceneInstaller>(session.gameObject);
                    Undo.RecordObject(installer, "Bind race VFX");
                    var serialized = new SerializedObject(installer);
                    // Opening a scene can unload the native asset created before the scene swap.
                    var sceneDefinition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(DefinitionPath);
                    if (sceneDefinition == null) throw new InvalidOperationException("Race VFX definition did not survive asset import.");
                    FillReference(serialized, "definition", sceneDefinition);
                    FillReference(serialized, "raceSessionInstaller", session);
                    FillReference(serialized, "outputCamera", follow.GetComponent<UnityEngine.Camera>());
                    if (created) serialized.FindProperty("enableRain").boolValue = name == "RaceSuzuka";
                    if (serialized.ApplyModifiedProperties() || created)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            Debug.Log("Missing VFX assets and references initialized; existing materials, particles and scene settings preserved.");
        }

        private static Material Material(string name, string shaderName, Action<Material> initialize = null)
        {
            var path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            if (System.IO.File.Exists(path)) throw new InvalidOperationException("Existing material cannot be loaded: " + path);
            var shader = Shader.Find(shaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader unavailable: " + shaderName);
            material = new Material(shader);
            initialize?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        [MenuItem("Apex Rivals/VFX/Add Tire Mark Material")]
        public static void AddTireMarks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before authoring materials.");
            var definition = AssetDatabase.LoadAssetAtPath<RaceVfxDefinition>(DefinitionPath);
            if (definition == null) throw new InvalidOperationException("Create the base race VFX assets first.");
            if (definition.skidmarkMaterial != null) return;
            Folder(Root + "Materials");
            var material = Material("TireMarks", "ApexRivals/VFX/Tire Marks");
            Undo.RecordObject(definition, "Add tire mark material");
            definition.skidmarkMaterial = material;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        private static Mesh CreateSmokeMesh()
        {
            var path = Root + "Meshes/FacetedSmoke.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            if (System.IO.File.Exists(path)) throw new InvalidOperationException("Existing smoke mesh cannot be loaded: " + path);
            // Eight independent triangles retain hard normals on the octahedron's faces.
            var points = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            var faces = new[] { 0,4,3, 0,2,4, 0,5,2, 0,3,5, 1,3,4, 1,4,2, 1,2,5, 1,5,3 };
            var vertices = new Vector3[faces.Length];
            var triangles = new int[faces.Length];
            for (var i = 0; i < faces.Length; i++) { vertices[i] = points[faces[i]] * 0.5f; triangles[i] = i; }
            mesh = new Mesh { name = "FacetedSmoke", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static ParticleSystem Prefab(string name, Material material, Mesh mesh, int kind)
        {
            var path = Root + "Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var particles = existing.GetComponent<ParticleSystem>();
                if (particles == null) throw new InvalidOperationException("Existing prefab has no ParticleSystem: " + path);
                return particles;
            }
            if (System.IO.File.Exists(path)) throw new InvalidOperationException("Existing prefab cannot be loaded: " + path);
            var root = new GameObject(name);
            try
            {
                if (kind == 2) root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = true;
                main.prewarm = kind == 2;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.maxParticles = kind == 2 ? 1200 : kind == 1 ? 120 : 100;
                main.startLifetime = kind == 0 ? new ParticleSystem.MinMaxCurve(0.65f, 1.2f)
                    : kind == 1 ? new ParticleSystem.MinMaxCurve(0.18f, 0.45f) : new ParticleSystem.MinMaxCurve(0.7f, 0.95f);
                main.startSpeed = kind == 0 ? new ParticleSystem.MinMaxCurve(0.2f, 0.55f)
                    : kind == 1 ? new ParticleSystem.MinMaxCurve(4f, 12f) : new ParticleSystem.MinMaxCurve(20f, 25f);
                main.startSize = kind == 0 ? new ParticleSystem.MinMaxCurve(0.3f, 0.65f)
                    : kind == 1 ? new ParticleSystem.MinMaxCurve(0.035f, 0.08f) : new ParticleSystem.MinMaxCurve(0.055f, 0.085f);
                main.startColor = kind == 0 ? new ParticleSystem.MinMaxGradient(new Color(0.55f,0.58f,0.6f,0.32f), new Color(0.85f,0.86f,0.88f,0.5f))
                    : kind == 1 ? new ParticleSystem.MinMaxGradient(new Color(1f,0.45f,0.08f), new Color(1f,0.85f,0.3f))
                    : new ParticleSystem.MinMaxGradient(new Color(0.5f,0.7f,0.85f,0.55f), new Color(0.75f,0.85f,1f,0.8f));
                main.gravityModifier = kind == 1 ? 1.2f : 0f;
                var emission = particles.emission;
                emission.rateOverTime = kind == 2 ? 700f : 0f;
                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = kind == 2 ? ParticleSystemShapeType.Box : kind == 1 ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
                shape.radius = kind == 0 ? 0.08f : 0.04f;
                shape.angle = 32f;
                if (kind == 2) shape.scale = new Vector3(28f, 24f, 4f);
                var color = particles.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(kind == 0 ? 0f : 1f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                color.color = gradient;
                if (kind == 0)
                {
                    main.startRotation3D = true;
                    main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    var size = particles.sizeOverLifetime;
                    size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.4f));
                    var velocity = particles.velocityOverLifetime;
                    velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.World;
                    velocity.y = 0.6f;
                }
                else
                {
                    var collision = particles.collision;
                    collision.enabled = true;
                    collision.type = ParticleSystemCollisionType.World;
                    collision.mode = ParticleSystemCollisionMode.Collision3D;
                    collision.quality = ParticleSystemCollisionQuality.Medium;
                    collision.enableDynamicColliders = true;
                    collision.collidesWith = ~0;
                    collision.lifetimeLoss = kind == 2 ? 1f : 0.4f;
                    collision.bounce = kind == 2 ? 0f : 0.25f;
                    collision.dampen = 0.5f;
                }
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.renderMode = kind == 0 ? ParticleSystemRenderMode.Mesh : ParticleSystemRenderMode.Stretch;
                renderer.maxParticleSize = kind == 2 ? 0.06f : kind == 1 ? 0.08f : 0.14f;
                if (kind == 0) renderer.mesh = mesh;
                else { renderer.lengthScale = kind == 1 ? 2f : 4f; renderer.velocityScale = kind == 1 ? 0.02f : 0.025f; }
                renderer.SetActiveVertexStreams(kind == 0
                    ? new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV }
                    : new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return prefab.GetComponent<ParticleSystem>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void FillReference(SerializedObject target, string name, UnityEngine.Object value)
        {
            var property = target.FindProperty(name);
            if (property.objectReferenceValue == null) property.objectReferenceValue = value;
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
