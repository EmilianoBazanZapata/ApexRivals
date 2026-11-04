using ApexRivals.AI.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Configuration
{
    /// <summary>
    /// Driver profile: how fast, precise, aggressive and error-prone one AI rival is, plus how it recovers.
    /// Every value that distinguishes difficulty lives here so a rival's skill is authored in one asset.
    /// </summary>
    [CreateAssetMenu(fileName = "AiDriverConfiguration", menuName = "Apex Rivals/AI/AI Driver Configuration")]
    public sealed class AiDriverConfiguration : ScriptableObject
    {
        [SerializeField]
        private string difficultyName = "Standard";

        [SerializeField, Min(AiDriverRules.MinimumDifficultyMultiplier)]
        private float difficultyMultiplier = 1f;

        [SerializeField, Min(0f)]
        private float targetSpeed = 22f;

        [SerializeField, Min(0f)]
        private float minimumCornerSpeed = 8f;





        [SerializeField, Min(0f)]
        private float stuckSpeedThreshold = 1.5f;

        [SerializeField, Min(0f)]
        private float stuckDetectionDuration = 2.5f;


        [SerializeField, Min(0f)]
        private float steeringSmoothing = 8f;

        [SerializeField, Min(0f)]
        private float throttleSmoothing = 6f;

        [SerializeField, Min(0f)]
        private float brakeSmoothing = 10f;



        [Header("Driving Skill")]
        [SerializeField, Tooltip("Experimental: derive a corner map and continuous braking profile from this circuit's racing line.")]
        private bool enableCircuitPlan;

        [SerializeField, Tooltip("Experimental CPU trajectory sampling with predicted rival movement.")]
        private bool enablePredictivePlanning;

        [SerializeField, Range(0.5f, 1.2f), Tooltip("Fraction of the ideal corner speed this driver carries.")]
        private float cornerSpeedFactor = 1f;

        [SerializeField, Min(0f), Tooltip("Higher values slow the driver more in tight corners.")]
        private float curvatureSpeedScale = 18f;

        [SerializeField, Min(0f), Tooltip("Lateral grip (m/s²) the driver trusts in corners; caps corner speed at sqrt(grip × radius). 0 disables the cap.")]
        private float corneringGrip = 9f;

        [SerializeField, Min(0f), Tooltip("How far ahead the driver looks for slower corners to brake for.")]
        private float brakingPreviewDistance = 80f;

        [SerializeField, Range(0.4f, 1.5f), Tooltip("Scales the braking deceleration the driver relies on; higher brakes later.")]
        private float brakingConfidence = 1f;

        [SerializeField, Min(0f)]
        private float throttleGain = 1.4f;

        [SerializeField, Min(0f)]
        private float brakeGain = 1.2f;

        [SerializeField, Min(0f)]
        private float headingGain = 1f;

        [SerializeField, Min(0f)]
        private float crossTrackGain = 1.6f;

        [SerializeField, Min(0f)]
        private float minimumLookahead = 10f;

        [SerializeField, Min(0f)]
        private float maximumLookahead = 18f;

        [SerializeField, Min(0f), Tooltip("Metres the driver drifts off the ideal line; 0 is a perfect line.")]
        private float lineWanderAmplitude;

        [SerializeField, Min(0f), Tooltip("Seconds between tactical re-evaluations; slower drivers react later.")]
        private float reactionTime = 0.15f;

        [SerializeField, Min(0f), Tooltip("Mean seconds between driving mistakes; 0 disables mistakes.")]
        private float mistakeInterval;

        [SerializeField, Min(0f)]
        private float mistakeDuration = 1.2f;

        [Header("Drift")]
        [SerializeField, Range(0f, 1f), Tooltip("How readily the driver drifts corners instead of braking for them: 0 never, 1 every corner of 50° or more.")]
        private float driftAffinity;

        [SerializeField, Range(0f, 1f), Tooltip("How cleanly the driver drifts: 1 holds the slide precisely every time; lower values vary the angle, correct less sharply and botch some drifts (over-rotating or kicking too softly).")]
        private float driftSkill = 1f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Extra corner speed carried toward a corner the driver will drift; the slide scrubs it off.")]
        private float driftEntrySpeedBonus = 0.1f;

        [SerializeField, Range(5f, 60f), Tooltip("Slide angle (degrees) the driver holds with the throttle.")]
        private float driftTargetSlipAngle = 25f;

        [SerializeField, Range(10f, 90f), Tooltip("Slide angle (degrees) past which the driver straightens up to avoid a spin.")]
        private float driftMaximumSlipAngle = 55f;

        [Header("Racecraft")]
        [SerializeField, Range(0f, 1f), Tooltip("Willingness to overtake, defend and close gaps.")]
        private float aggression = 0.5f;

        [SerializeField, Min(0f), Tooltip("Seconds of gap kept behind a slower car while following.")]
        private float followTimeGap = 0.6f;

        [SerializeField, Min(0f), Tooltip("Lateral metres used to pass a car.")]
        private float overtakeLateralOffset = 3f;

        [SerializeField, Min(0f), Tooltip("Largest lateral offset from the ideal line the track allows.")]
        private float maximumLateralOffset = 4f;

        [Header("Recovery")]
        [SerializeField, Min(0f), Tooltip("Distance from the ideal line considered off track.")]
        private float offTrackDistance = 10f;

        [SerializeField, Min(0f), Tooltip("Seconds off track before the driver resets.")]
        private float offTrackResetDuration = 3f;

        [SerializeField, Min(0f)]
        private float reverseDuration = 1.2f;

        [SerializeField, Min(0f)]
        private float postResetGraceDuration = 2f;

        public string DifficultyName => difficultyName;
        public float DifficultyMultiplier => difficultyMultiplier;

        public float CornerSpeedFactor => cornerSpeedFactor;
        public bool EnableCircuitPlan => enableCircuitPlan;
        public bool EnablePredictivePlanning => enablePredictivePlanning;
        public float CurvatureSpeedScale => curvatureSpeedScale;
        public float CorneringGrip => corneringGrip;
        public float BrakingPreviewDistance => brakingPreviewDistance;
        public float BrakingConfidence => brakingConfidence;
        public float ThrottleGain => throttleGain;
        public float BrakeGain => brakeGain;
        public float HeadingGain => headingGain;
        public float CrossTrackGain => crossTrackGain;
        public float MinimumLookahead => minimumLookahead;
        public float MaximumLookahead => maximumLookahead;
        public float LineWanderAmplitude => lineWanderAmplitude;
        public float ReactionTime => reactionTime;
        public float MistakeInterval => mistakeInterval;
        public float MistakeDuration => mistakeDuration;

        public float DriftAffinity => driftAffinity;
        public float DriftSkill => driftSkill;
        public float DriftEntrySpeedBonus => driftEntrySpeedBonus;
        public float DriftTargetSlipAngle => driftTargetSlipAngle;
        public float DriftMaximumSlipAngle => driftMaximumSlipAngle;

        public float Aggression => aggression;
        public float FollowTimeGap => followTimeGap;
        public float OvertakeLateralOffset => overtakeLateralOffset;
        public float MaximumLateralOffset => maximumLateralOffset;

        public float OffTrackDistance => offTrackDistance;
        public float OffTrackResetDuration => offTrackResetDuration;
        public float ReverseDuration => reverseDuration;
        public float PostResetGraceDuration => postResetGraceDuration;

        public AiDriverTuning CreateTuning()
        {
            return new AiDriverTuning(
                AiDriverRules.ApplyDifficulty(targetSpeed, difficultyMultiplier),
                Mathf.Min(minimumCornerSpeed, AiDriverRules.ApplyDifficulty(targetSpeed, difficultyMultiplier)),
                stuckSpeedThreshold,
                stuckDetectionDuration,
                steeringSmoothing,
                throttleSmoothing,
                brakeSmoothing);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(difficultyName))
            {
                difficultyName = "Standard";
            }

            difficultyMultiplier = Mathf.Max(AiDriverRules.MinimumDifficultyMultiplier, difficultyMultiplier);
            targetSpeed = Mathf.Max(0f, targetSpeed);
            minimumCornerSpeed = Mathf.Clamp(minimumCornerSpeed, 0f, targetSpeed);
            stuckSpeedThreshold = Mathf.Max(0f, stuckSpeedThreshold);
            stuckDetectionDuration = Mathf.Max(0f, stuckDetectionDuration);
            steeringSmoothing = Mathf.Max(0f, steeringSmoothing);
            throttleSmoothing = Mathf.Max(0f, throttleSmoothing);
            brakeSmoothing = Mathf.Max(0f, brakeSmoothing);
            maximumLookahead = Mathf.Max(minimumLookahead, maximumLookahead);
            maximumLateralOffset = Mathf.Max(0f, maximumLateralOffset);
            overtakeLateralOffset = Mathf.Min(overtakeLateralOffset, maximumLateralOffset);
            driftMaximumSlipAngle = Mathf.Max(driftTargetSlipAngle + 1f, driftMaximumSlipAngle);
        }
    }
}
