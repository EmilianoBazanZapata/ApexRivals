using System;
using UnityEngine;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Engine geometry for <see cref="ProceduralEngineSynth"/>: when each cylinder fires (crank angle within the
    /// 720° four-stroke cycle), which exhaust bank it feeds, and how that exhaust and intake are built. The firing
    /// rhythm per bank gives each layout its character: a cross-plane V8 fires each bank unevenly (the burble), a
    /// flat-plane V8 or an inline four evenly (a smooth howl).
    /// </summary>
    [CreateAssetMenu(menuName = "ApexRivals/Audio/Engine Sound Profile")]
    public sealed class EngineSoundProfile : ScriptableObject
    {
        [SerializeField] private string displayName = "Engine";
        [SerializeField, TextArea] private string description = string.Empty;

        [Header("Firing")]
        [SerializeField, Tooltip("Cylinder numbers in firing order, for display (e.g. 1-8-4-3-6-5-7-2).")]
        private int[] firingOrder = Array.Empty<int>();
        [SerializeField, Tooltip("Crank angle (0-720°) of each firing event, in the same order as the firing order.")]
        private float[] firingAngles = Array.Empty<float>();
        [SerializeField, Tooltip("Exhaust bank (0 or 1) each firing event feeds, in the same order as the firing order.")]
        private int[] firingBanks = Array.Empty<int>();

        [Header("Engine")]
        [SerializeField, Min(300f)] private float idleRpm = 800f;
        [SerializeField, Min(1000f)] private float redlineRpm = 7000f;
        [SerializeField, Min(0.5f), Tooltip("Displacement in litres: bigger engines fire harder and lower.")] private float displacement = 5f;

        [Header("Exhaust")]
        [SerializeField, Tooltip("Length in metres of each bank's pipe from the cylinder to the collector; sets its resonance.")]
        private float[] pipeLengths = { 0.9f, 0.9f };
        [SerializeField, Range(0f, 0.9f), Tooltip("How long the pipe rings after each pulse.")] private float pipeFeedback = 0.55f;
        [SerializeField, Min(100f), Tooltip("Muffler cut-off: lower is deeper and more muffled.")] private float mufflerCutoff = 1100f;
        [SerializeField, Min(40f), Tooltip("Collector/tailpipe body resonance.")] private float collectorResonance = 180f;
        [SerializeField, Range(0f, 1f), Tooltip("Cycle-to-cycle variation of each combustion: roughness and lope.")] private float roughness = 0.12f;

        [Header("Other sources")]
        [SerializeField, Range(0f, 1f)] private float intakeLevel = 0.25f;
        [SerializeField, Range(0f, 1f)] private float mechanicalLevel = 0.08f;
        [SerializeField, Range(0f, 1f), Tooltip("Turbo whine and spool (0 for naturally aspirated).")] private float turboLevel;
        [SerializeField, Range(0f, 1f), Tooltip("Chance of an exhaust pop on each firing event off the throttle at high rpm.")] private float afterfire = 0.04f;

        public string DisplayName => displayName;
        public string Description => description;
        public int[] FiringOrder => firingOrder;
        public float[] FiringAngles => firingAngles;
        public int[] FiringBanks => firingBanks;
        public float IdleRpm => idleRpm;
        public float RedlineRpm => redlineRpm;
        public float Displacement => displacement;
        public float[] PipeLengths => pipeLengths;
        public float PipeFeedback => pipeFeedback;
        public float MufflerCutoff => mufflerCutoff;
        public float CollectorResonance => collectorResonance;
        public float Roughness => roughness;
        public float IntakeLevel => intakeLevel;
        public float MechanicalLevel => mechanicalLevel;
        public float TurboLevel => turboLevel;
        public float Afterfire => afterfire;
        public bool IsTurbo => turboLevel > 0f;

        /// <summary>Firing events per second at <paramref name="rpm"/> (one cycle = two crank revolutions).</summary>
        public float FiringFrequency(float rpm) => rpm / 120f * Mathf.Max(1, firingAngles.Length);

        /// <summary>Editor setup: fills the whole profile in one call.</summary>
        public void Configure(string name, string text, int[] order, float[] angles, int[] banks, float idle, float redline,
            float litres, float[] pipes, float feedback, float muffler, float collector, float rough, float intake,
            float mechanical, float turbo, float pops)
        {
            displayName = name;
            description = text;
            firingOrder = order;
            firingAngles = angles;
            firingBanks = banks;
            idleRpm = idle;
            redlineRpm = redline;
            displacement = litres;
            pipeLengths = pipes;
            pipeFeedback = feedback;
            mufflerCutoff = muffler;
            collectorResonance = collector;
            roughness = rough;
            intakeLevel = intake;
            mechanicalLevel = mechanical;
            turboLevel = turbo;
            afterfire = pops;
        }
    }
}
