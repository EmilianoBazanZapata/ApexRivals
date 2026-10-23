using ApexRivals.AI.Configuration;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>Vehicle-level limits shared by every driver profile.</summary>
    public readonly struct AiControlSettings
    {
        public AiControlSettings(
            float speedSoftening,
            float brakingDeceleration,
            float maximumBrake,
            float rejoinMinimumLookahead,
            float rejoinTargetSpeed,
            float rejoinSteeringLimit)
        {
            SpeedSoftening = speedSoftening;
            BrakingDeceleration = brakingDeceleration;
            MaximumBrake = maximumBrake;
            RejoinMinimumLookahead = rejoinMinimumLookahead;
            RejoinTargetSpeed = rejoinTargetSpeed;
            RejoinSteeringLimit = rejoinSteeringLimit;
        }

        public float SpeedSoftening { get; }

        /// <summary>Deceleration the vehicle sustains under the strongest brake the driver uses (m/s²).</summary>
        public float BrakingDeceleration { get; }

        public float MaximumBrake { get; }
        public float RejoinMinimumLookahead { get; }
        public float RejoinTargetSpeed { get; }
        public float RejoinSteeringLimit { get; }
    }

    /// <summary>
    /// Turns the plan into pedal and wheel inputs: speed from the corner and braking profile capped by
    /// traffic and obstacles, steering toward a laterally offset point ahead on the ideal path, then
    /// the stability aids (low-speed steering cap, launch control, brake/steer blend, slide easing)
    /// and input smoothing.
    /// </summary>
    public sealed class AiVehicleControl
    {
        /// <summary>Share of the path-following steer kept during a drift; the slide control does the rest.</summary>
        private const float DriftPathSteeringWeight = 0.5f;

        private float _smoothedSteering;
        private float _smoothedThrottle;
        private float _smoothedBrake;

        public DrivingInput Input { get; private set; } = DrivingInput.Neutral;
        public float SteeringCommand { get; private set; }
        public float ThrottleCommand { get; private set; }
        public float BrakeCommand { get; private set; }
        public float TargetSpeed { get; private set; }

        /// <summary>Speed the track allows here without traffic or obstacles.</summary>
        public float FreeSpeed { get; private set; }

        /// <summary>The driver's straight-line speed with the current corner factor.</summary>
        public float StraightSpeed { get; private set; }

        public Vector3 SteeringTarget { get; private set; }

        public void Reset()
        {
            Input = DrivingInput.Neutral;
            _smoothedSteering = 0f;
            _smoothedThrottle = 0f;
            _smoothedBrake = 0f;
            SteeringCommand = 0f;
            ThrottleCommand = 0f;
            BrakeCommand = 0f;
            TargetSpeed = 0f;
        }

        public void Drive(
            AiPathTracker tracker,
            Transform vehicle,
            float speed,
            float lateralSpeed,
            float lateralOffset,
            float trafficSpeedLimit,
            float obstacleSpeedLimit,
            bool rejoining,
            AiDriverPersonality personality,
            AiDriftController drift,
            AiDriverConfiguration configuration,
            AiDriverTuning tuning,
            in AiControlSettings settings,
            float deltaTime)
        {
            var path = tracker.Path;
            var projection = tracker.Projection;
            // Toward a corner it will drift the driver carries extra speed; the slide scrubs it off.
            var driftBonus = drift.IsArmed ? 1f + configuration.DriftEntrySpeedBonus : 1f;
            var cornerFactor = configuration.CornerSpeedFactor * personality.CornerSpeedMultiplier * driftBonus;
            var confidence = configuration.BrakingConfidence * personality.BrakingConfidenceMultiplier;
            var sample = path.GetSample(projection.SegmentIndex);
            var brakingLimit = path.GetBrakingLimitedSpeed(projection, configuration.BrakingPreviewDistance, settings.BrakingDeceleration * confidence, cornerFactor);
            FreeSpeed = Mathf.Min(sample.TargetSpeed * cornerFactor, brakingLimit);
            StraightSpeed = tuning.TargetSpeed * cornerFactor;

            var targetSpeed = Mathf.Min(FreeSpeed, Mathf.Min(trafficSpeedLimit, obstacleSpeedLimit));
            if (rejoining)
            {
                targetSpeed = Mathf.Min(targetSpeed, settings.RejoinTargetSpeed);
            }

            TargetSpeed = targetSpeed;

            var lookahead = rejoining
                ? AiDriverRules.CalculateRejoinLookahead(projection.Distance, settings.RejoinMinimumLookahead)
                : IdealRacingDriverRules.CalculateLookahead(speed, Mathf.Max(0.1f, tuning.TargetSpeed), sample.Curvature, configuration.MinimumLookahead, configuration.MaximumLookahead);
            var aheadPoint = path.GetPointAhead(projection, lookahead, out var aheadIndex, out _);
            var right = Vector3.Cross(Vector3.up, path.GetSample(aheadIndex).Forward).normalized;
            SteeringTarget = aheadPoint + right * lateralOffset;

            var steeringDirection = Vector3.ProjectOnPlane(SteeringTarget - vehicle.position, Vector3.up);
            if (steeringDirection.sqrMagnitude < 0.0001f)
            {
                steeringDirection = projection.Tangent;
            }

            // In a drift the nose no longer shows where the car goes: steer the direction of travel instead
            // and let the vehicle's countersteer follow the slide.
            var travelDirection = drift.IsDrifting
                ? vehicle.forward * Mathf.Max(0.5f, speed) + vehicle.right * lateralSpeed
                : vehicle.forward;
            var headingError = IdealRacingDriverRules.CalculateSignedHeadingError(travelDirection, steeringDirection);
            var steering = IdealRacingDriverRules.CalculateSteering(
                headingError,
                projection.CrossTrackError - lateralOffset,
                speed,
                configuration.HeadingGain,
                configuration.CrossTrackGain,
                settings.SpeedSoftening);
            steering = AiDriverRules.LimitLowSpeedSteering(steering, speed);
            if (rejoining)
            {
                steering = Mathf.Clamp(steering, -settings.RejoinSteeringLimit, settings.RejoinSteeringLimit);
            }

            SteeringCommand = steering;
            if (drift.IsDrifting)
            {
                // The kick: full lock into the corner while the rear lets go, as a driver flicks it in. After
                // it, follow the road with the travel direction and hold the slide angle on top.
                SteeringCommand = drift.ButtonPressed
                    ? drift.CornerDirection
                    : Mathf.Clamp(DriftPathSteeringWeight * steering + drift.SteeringCorrection, -1f, 1f);

                // The slide is the brake: power holds the angle and the vehicle countersteers.
                ThrottleCommand = drift.Throttle;
                BrakeCommand = 0f;
                ApplySmoothed(tuning, deltaTime, drift.ButtonPressed);
                return;
            }

            var speedCommand = IdealRacingDriverRules.CalculateSpeedCommand(speed, targetSpeed, configuration.ThrottleGain, configuration.BrakeGain);
            var slideScale = Mathf.Min(AiDriverRules.CalculateSlideInputScale(lateralSpeed), AiDriverRules.CalculateTractionScale(drift.SlipAngle));
            ThrottleCommand = AiDriverRules.LimitLaunchThrottle(speedCommand.Throttle, speed) * slideScale;
            BrakeCommand = Mathf.Min(settings.MaximumBrake, AiDriverRules.BlendBrakeWithSteering(speedCommand.Brake, steering)) * slideScale;
            ApplySmoothed(tuning, deltaTime, false);
        }

        /// <summary>Reverse out of a trap: brake below the reverse-entry speed and steer the nose back toward the line.</summary>
        public void Reverse(Transform vehicle, Vector3 pathTangent, AiDriverTuning tuning, float deltaTime)
        {
            var headingError = IdealRacingDriverRules.CalculateSignedHeadingError(vehicle.forward, pathTangent);
            SteeringCommand = AiRecoveryRules.CalculateReverseSteering(headingError);
            ThrottleCommand = 0f;
            BrakeCommand = 1f;
            TargetSpeed = 0f;
            ApplySmoothed(tuning, deltaTime, false);
        }

        /// <summary>Drops any throttle carried over so reversing starts cleanly.</summary>
        public void CutThrottle()
        {
            _smoothedThrottle = 0f;
        }

        /// <summary>Asks the vehicle controller to reset the car (pose and drivetrain) on its next physics step.</summary>
        public void RequestVehicleReset()
        {
            Input = new DrivingInput(0f, 0f, 0f, false, true);
        }

        public void ConsumeResetRequest()
        {
            Input = Input.WithoutResetRequest();
        }

        public void ConsumeRecoveryRequest()
        {
            Input = Input.WithoutRecoveryRequest();
        }

        public void SetNeutral()
        {
            Input = DrivingInput.Neutral;
        }

        private void ApplySmoothed(AiDriverTuning tuning, float deltaTime, bool drift)
        {
            _smoothedSteering = Smooth(_smoothedSteering, SteeringCommand, tuning.SteeringSmoothing, deltaTime);
            _smoothedThrottle = AiDriverRules.SmoothPedal(_smoothedThrottle, ThrottleCommand, tuning.ThrottleSmoothing, deltaTime);
            _smoothedBrake = AiDriverRules.SmoothPedal(_smoothedBrake, BrakeCommand, tuning.BrakeSmoothing, deltaTime);
            Input = new DrivingInput(_smoothedThrottle, _smoothedBrake, _smoothedSteering, drift, false);
        }

        private static float Smooth(float current, float target, float smoothing, float deltaTime)
        {
            return smoothing <= 0f ? target : Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * deltaTime));
        }
    }
}
