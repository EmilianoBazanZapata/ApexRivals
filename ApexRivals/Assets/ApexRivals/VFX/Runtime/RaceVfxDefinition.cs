using UnityEngine;

namespace ApexRivals.VFX.Runtime
{
    [CreateAssetMenu(menuName = "Apex Rivals/Race VFX")]
    public sealed class RaceVfxDefinition : ScriptableObject
    {
        public ParticleSystem driftSmokePrefab;
        public ParticleSystem impactSparksPrefab;
        public ParticleSystem rainPrefab;
        public Material confettiMaterial;
        [Tooltip("Victory confetti overlay: a screen-space canvas holding a VictoryConfettiGraphic.")]
        public GameObject confettiPrefab;
        public Material skidmarkMaterial;
        [Header("Tire marks")]
        [Min(0f)] public float skidMinimumSpeed = 4f;
        [Range(0f, 1f)] public float skidHardBrakeThreshold = 0.65f;
        [Range(0.08f, 0.5f)] public float skidWidth = 0.22f;
        [Range(0f, 1f)] public float skidOpacity = 0.8f;
        [Range(0.1f, 1f)] public float skidMinimumSpacing = 0.25f;
        [Min(0.5f)] public float skidMaximumConnection = 2.5f;
        [Min(1f)] public float skidLifetime = 25f;
        [Range(128, 8192)] public int skidMaximumSegments = 4096;
        [Header("Drift smoke")]
        [Min(0f)] public float minimumSmokeSpeed = 6f;
        [Range(1f, 60f)] public float minimumSlipAngle = 9f;
        [Min(0f)] public float minimumWheelSlip = 0.25f;
        [Range(1f, 60f)] public float smokeRatePerWheel = 24f;
        [Header("Impact sparks")]
        [Min(0f)] public float minimumImpactSpeed = 2f;
        [Min(0.02f)] public float impactCooldown = 0.15f;
        [Range(6, 80)] public int maximumSparkCount = 40;
        [Tooltip("Sparks from hitting another car.")] public Color vehicleImpactSparkColor = new Color(1f, 0.48f, 0.08f);
        [Tooltip("Sparks from hitting a wall or barrier.")] public Color wallImpactSparkColor = new Color(1f, 0.78f, 0.3f);
    }
}
