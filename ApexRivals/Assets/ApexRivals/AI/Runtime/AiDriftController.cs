using ApexRivals.AI.Configuration;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// Drift state for one driver. Arms when a corner worth drifting lies within the braking preview (the
    /// driver then carries more speed toward it instead of braking down to grip speed), then drives the
    /// drift in three phases: a handbrake kick with the wheel turned in, a sustained slide held at the
    /// profile's angle with steering and throttle, and a straighten-up that only ends once the car points
    /// down the road again.
    /// </summary>
    public sealed class AiDriftController
    {
        private const float EntryLeadSeconds = 0.2f;
        private const float CornerWindow = 25f;
        private const float ArmScanStep = 10f;
        private const float ExitWindow = 20f;
        private const float ExitCornerAngle = 20f;
        private const float MinimumEntrySpeed = 15f;
        private const float SettledSlipAngle = 10f;
        private const float ExitSpeed = 7f;
        private const float MaximumDuration = 3.5f;
        private const float MaximumStraightenDuration = 1.2f;
        private const float CooldownDuration = 0.8f;
        private const float MinimumDriftThrottle = 0.35f;
        private const float StraightenThrottle = 0.5f;
        private const float MaximumPulse = 0.6f;
        private const int MaximumKicks = 3;
        private const float OverRotationExtraSlip = 15f;
        private const float OverRotationGainScale = 0.5f;
        private const float ShortKickPulse = 0.15f;

        private System.Random _random = new System.Random(1);
        private float _slipTarget;
        private float _gainScale = 1f;
        private float _kickPulse = MaximumPulse;

        private float _elapsed;
        private float _phaseTime;
        private float _cooldown;
        private float _cornerAngle;
        private bool _inCorner;
        private int _kicks;

        public bool IsArmed { get; private set; }
        public bool IsDrifting => Phase != AiDriftPhase.None;
        public AiDriftPhase Phase { get; private set; }
        public float SlipAngle { get; private set; }

        /// <summary>Yaw rate in degrees per second; positive turns right.</summary>
        public float YawRate { get; private set; }

        /// <summary>+1 for a right-hand corner, -1 for a left-hander: the way the tail is kicked.</summary>
        public float CornerDirection => _cornerAngle >= 0f ? 1f : -1f;

        /// <summary>Whether the drift (handbrake) button is pressed this frame: only during a kick.</summary>
        public bool ButtonPressed => Phase == AiDriftPhase.Kick;

        /// <summary>Steering added to the path-following steer to hold the slide angle and damp the rotation.</summary>
        public float SteeringCorrection { get; private set; }

        public float Throttle { get; private set; }
        public int DriftCount { get; private set; }

        /// <summary>Drifts this driver botched on purpose of its skill (over-rotated or under-kicked).</summary>
        public int MistakeCount { get; private set; }

        /// <summary>Seeds the driver's drift errors so a rival makes the same mistakes every race.</summary>
        public void Seed(string racerId)
        {
            var seed = 23;
            if (!string.IsNullOrEmpty(racerId))
            {
                for (var index = 0; index < racerId.Length; index++)
                {
                    seed = seed * 31 + racerId[index];
                }
            }

            _random = new System.Random(seed);
        }

        public void Reset()
        {
            IsArmed = false;
            Phase = AiDriftPhase.None;
            SteeringCorrection = 0f;
            Throttle = 0f;
            _elapsed = 0f;
            _phaseTime = 0f;
            _cooldown = 0f;
        }

        public void Update(
            IdealRacingPath path,
            IdealRacingPath.Projection projection,
            float speed,
            float forwardSpeed,
            float lateralSpeed,
            float yawRate,
            bool allowed,
            AiDriverConfiguration configuration,
            float deltaTime)
        {
            SlipAngle = AiDriftRules.CalculateSlipAngle(forwardSpeed, lateralSpeed);
            YawRate = yawRate;
            _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
            var minimumCornerAngle = AiDriftRules.CalculateMinimumCornerAngle(configuration.DriftAffinity);
            if (!allowed || path == null || float.IsPositiveInfinity(minimumCornerAngle))
            {
                Stop(false);
                IsArmed = false;
                return;
            }

            if (IsDrifting)
            {
                UpdateDrift(path, projection, speed, configuration, deltaTime);
                IsArmed = IsDrifting;
                return;
            }

            var lead = speed * EntryLeadSeconds;
            var entryCorner = path.GetHeadingChange(projection, lead, lead + CornerWindow);
            var settled = Mathf.Abs(SlipAngle) < SettledSlipAngle;
            var cornerSpeed = path.GetMinimumTargetSpeed(projection, lead, lead + CornerWindow) * configuration.CornerSpeedFactor;
            if (_cooldown <= 0f && settled && speed >= MinimumEntrySpeed && AiDriftRules.IsDriftCorner(entryCorner, minimumCornerAngle, cornerSpeed))
            {
                _cornerAngle = entryCorner;
                _inCorner = false;
                _elapsed = 0f;
                _kicks = 0;
                DriftCount++;
                PlanDrift(configuration);
                BeginKick();
                IsArmed = true;
                return;
            }

            IsArmed = IsDriftCornerWithin(path, projection, configuration.BrakingPreviewDistance, minimumCornerAngle, configuration.CornerSpeedFactor);
        }

        private void UpdateDrift(IdealRacingPath path, IdealRacingPath.Projection projection, float speed, AiDriverConfiguration configuration, float deltaTime)
        {
            _elapsed += deltaTime;
            _phaseTime += deltaTime;
            var direction = CornerDirection;
            var slipIntoCorner = AiDriftRules.CalculateSlipIntoCorner(SlipAngle, _cornerAngle);
            var yawIntoCorner = YawRate * direction;
            var target = _slipTarget;

            var remaining = path.GetHeadingChange(projection, 0f, ExitWindow);
            // The drift starts a little before the corner; only judge the exit once the corner is underway.
            _inCorner |= Mathf.Abs(remaining) >= ExitCornerAngle;
            if (!_inCorner)
            {
                remaining = _cornerAngle;
            }

            if (Phase != AiDriftPhase.Straighten
                && AiDriftRules.ShouldExitDrift(remaining, ExitCornerAngle, slipIntoCorner, configuration.DriftMaximumSlipAngle, speed, ExitSpeed, _elapsed, MaximumDuration))
            {
                SetPhase(AiDriftPhase.Straighten);
            }

            switch (Phase)
            {
                case AiDriftPhase.Kick:
                    SteeringCorrection = 0f;
                    Throttle = 1f;
                    if (AiDriftRules.ShouldEndKick(slipIntoCorner, target, yawIntoCorner, _phaseTime, _kickPulse))
                    {
                        SetPhase(AiDriftPhase.Sustain);
                    }

                    break;

                case AiDriftPhase.Sustain:
                    SteeringCorrection = AiDriftRules.CalculateSlideSteering(slipIntoCorner, target, yawIntoCorner, direction, _gainScale);
                    Throttle = AiDriftRules.CalculateDriftThrottle(slipIntoCorner, target, configuration.DriftMaximumSlipAngle, MinimumDriftThrottle);
                    if (_kicks < MaximumKicks && AiDriftRules.ShouldKickAgain(slipIntoCorner, target, yawIntoCorner, _phaseTime))
                    {
                        BeginKick();
                    }

                    break;

                case AiDriftPhase.Straighten:
                    SteeringCorrection = AiDriftRules.CalculateSlideSteering(slipIntoCorner, 0f, yawIntoCorner, direction, _gainScale);
                    Throttle = StraightenThrottle;
                    if (AiDriftRules.IsStraight(SlipAngle, YawRate) || _phaseTime > MaximumStraightenDuration)
                    {
                        Stop(true);
                    }

                    break;
            }
        }

        /// <summary>
        /// Decides how this drift will go. A perfect drifter holds the profile's angle every time; a less
        /// skilled one varies it, corrects less sharply and sometimes botches the drift: it over-rotates
        /// (too much angle, caught late) or kicks too softly and runs wide.
        /// </summary>
        private void PlanDrift(AiDriverConfiguration configuration)
        {
            var skill = configuration.DriftSkill;
            _slipTarget = AiDriftRules.CalculateDriftSlipTarget(configuration.DriftTargetSlipAngle, skill, (float)(_random.NextDouble() * 2.0 - 1.0));
            _gainScale = AiDriftRules.CalculateSkillGainScale(skill);
            _kickPulse = MaximumPulse;
            if (_random.NextDouble() >= AiDriftRules.CalculateMistakeChance(skill))
            {
                return;
            }

            MistakeCount++;
            if (_random.NextDouble() < 0.5)
            {
                _slipTarget += OverRotationExtraSlip;
                _gainScale *= OverRotationGainScale;
            }
            else
            {
                _kickPulse = ShortKickPulse;
            }
        }

        private void BeginKick()
        {
            _kicks++;
            SetPhase(AiDriftPhase.Kick);
            SteeringCorrection = 0f;
            Throttle = 1f;
        }

        private void SetPhase(AiDriftPhase phase)
        {
            Phase = phase;
            _phaseTime = 0f;
        }

        private static bool IsDriftCornerWithin(IdealRacingPath path, IdealRacingPath.Projection projection, float preview, float minimumCornerAngle, float cornerSpeedFactor)
        {
            for (var start = 0f; start <= preview; start += ArmScanStep)
            {
                var cornerSpeed = path.GetMinimumTargetSpeed(projection, start, start + CornerWindow) * cornerSpeedFactor;
                if (AiDriftRules.IsDriftCorner(path.GetHeadingChange(projection, start, start + CornerWindow), minimumCornerAngle, cornerSpeed))
                {
                    return true;
                }
            }

            return false;
        }

        private void Stop(bool startCooldown)
        {
            if (IsDrifting && startCooldown)
            {
                _cooldown = CooldownDuration;
            }

            Phase = AiDriftPhase.None;
            SteeringCorrection = 0f;
            Throttle = 0f;
            _elapsed = 0f;
            _phaseTime = 0f;
        }
    }
}
