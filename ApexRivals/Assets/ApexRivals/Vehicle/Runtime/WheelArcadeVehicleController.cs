using ApexRivals.Vehicle.Configuration;
using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WheelArcadeVehicleController : MonoBehaviour, IVehicleRuntime, IVehicleTelemetry
    {
        // Presentation-only threshold for the HUD's reverse ("R") gear text; distinct
        // from the reverseEntrySpeed tuning field, which governs when reverse motor/
        // brake input is actually accepted (see VehicleDriveInputRules).
        private const float ReversingDisplaySpeedThreshold = 0.05f;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Wheel-vehicle configuration used only by this experimental WheelCollider prototype.")]
        private WheelVehicleConfiguration configuration;

        [SerializeField]
        [Tooltip("Existing Apex component that provides DrivingInput. The controller never reads keyboard or gamepad devices directly.")]
        private MonoBehaviour inputProviderComponent;

        [SerializeField]
        [Tooltip("Optional existing reset component used when the shared DrivingInput requests a reset.")]
        private VehicleResetter resetter;

        [Header("Runtime Tuning Overrides")]
        [SerializeField]
        [Tooltip("Uses the Inspector-editable Runtime Tuning Overrides below instead of the configuration asset. Disable to make the assigned WheelVehicleConfiguration asset the single source of tuning values.")]
        private bool useRuntimeTuningOverrides;

        [SerializeField]
        [Tooltip("Developer-only live tuning snapshot. It is used only while Use Runtime Tuning Overrides is enabled and is initialized from the assigned configuration asset.")]
        private WheelVehicleTuning runtimeTuningOverrides;

        [Header("Composition")]
        [SerializeField]
        [Tooltip("Child transform exposed to race and scene composition as the vehicle camera target.")]
        private Transform cameraTarget;

        [Header("Wheel Physics")]
        [SerializeField]
        [Tooltip("Front-left WheelCollider. It receives steering and front-biased normal brake torque.")]
        private WheelCollider frontLeftWheel;

        [SerializeField]
        [Tooltip("Front-right WheelCollider. It receives steering and front-biased normal brake torque.")]
        private WheelCollider frontRightWheel;

        [SerializeField]
        [Tooltip("Rear-left WheelCollider. It receives RWD motor torque, rear normal braking, and handbrake torque.")]
        private WheelCollider rearLeftWheel;

        [SerializeField]
        [Tooltip("Rear-right WheelCollider. It receives RWD motor torque, rear normal braking, and handbrake torque.")]
        private WheelCollider rearRightWheel;

        [SerializeField, Min(0f)]
        [Tooltip("Speed (m/s) below which the wheels use the low-speed substep count.")]
        private float lowSpeedSubstepThreshold = 5f;

        [SerializeField, Min(1)]
        [Tooltip("Physics substeps per step for the wheels below the threshold speed.")]
        private int substepsBelowThreshold = 12;

        [SerializeField, Min(1)]
        [Tooltip("Physics substeps per step for the wheels above the threshold speed.")]
        private int substepsAboveThreshold = 15;

        [Header("Wheel Visuals")]
        [SerializeField]
        [Tooltip("Visual transform synchronized from the front-left WheelCollider world pose.")]
        private Transform frontLeftVisual;

        [SerializeField]
        [Tooltip("Visual transform synchronized from the front-right WheelCollider world pose.")]
        private Transform frontRightVisual;

        [SerializeField]
        [Tooltip("Visual transform synchronized from the rear-left WheelCollider world pose.")]
        private Transform rearLeftVisual;

        [SerializeField]
        [Tooltip("Visual transform synchronized from the rear-right WheelCollider world pose.")]
        private Transform rearRightVisual;

        private Rigidbody _rigidbody;
        private float _driftAssistSessionRemaining;
        private IDrivingInputProvider _inputProvider;
        private float _engineRpm;
        private float _tractionFactor = 1f;
        private float _shiftTimeRemaining;
        private float _shiftCooldownRemaining;
        private float _driftHandbrakeEntryTimeRemaining;
        private float _appliedRearSidewaysFrictionStiffness = float.NaN;
        private int _currentGearIndex;
        private bool _wasForwardDriftActive;
        private WheelVehicleTuning _effectiveTuning;
        private WheelVehicleTuning _lastAppliedPhysicsTuning;
        private bool _hasAppliedPhysicsTuning;
        private VehiclePerformanceStats _currentPerformanceStats;
        private bool _performanceStatsApplied;
        private float _performanceSteeringMultiplier = 1f;

        public float CurrentForwardSpeed { get; private set; }
        public float CurrentLateralSpeed { get; private set; }
        public float CurrentPlanarSpeed { get; private set; }
        public float CurrentVehicleSlipAngle { get; private set; }
        public float CurrentSteeringInput { get; private set; }
        public float CurrentFrontWheelSteerAngle { get; private set; }
        public float CurrentCountersteerCorrection { get; private set; }
        public float CurrentDriftAssistAcceleration { get; private set; }
        public bool DriftAssistActive => _driftAssistSessionRemaining > 0f;
        public float CurrentMotorTorque { get; private set; }
        /// <summary>Share of forward drive torque kept by traction control (1 = no intervention).</summary>
        public float CurrentTractionFactor => _tractionFactor;
        public float CurrentNormalBrakeTorque { get; private set; }
        public float CurrentHandbrakeTorque { get; private set; }
        public float CurrentSteeringSpeed { get; private set; }
        public float CurrentEngineRpm => _engineRpm;
        public int CurrentGear => _currentGearIndex + 1;
        public float RearLeftWheelRpm { get; private set; }
        public float RearRightWheelRpm { get; private set; }
        public float CurrentAverageDrivenWheelRpm { get; private set; }
        public float CurrentGearRatio { get; private set; }
        public float CurrentRawCalculatedEngineRpm { get; private set; }
        /// <summary>Engine RPM the automatic gearbox decides on (driven-wheel RPM capped at road speed).</summary>
        public float CurrentShiftEngineRpm { get; private set; }
        public float CurrentPowerCurveEvaluation { get; private set; }
        public float CurrentTorqueBeforeShiftSuppression { get; private set; }
        public bool ShiftInProgress => _shiftTimeRemaining > 0f;
        public float ShiftCooldownRemaining => _shiftCooldownRemaining;
        public int CurrentRequestedGear { get; private set; } = 1;
        public bool HandbrakeActive { get; private set; }
        public bool ForwardDriftActive { get; private set; }
        public float CurrentDriftHandbrakeMultiplier { get; private set; }
        public float CurrentRearSidewaysFrictionStiffness { get; private set; }
        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;
        public float FrontLeftForwardSlip { get; private set; }
        public float FrontLeftSidewaysSlip { get; private set; }
        public float FrontRightForwardSlip { get; private set; }
        public float FrontRightSidewaysSlip { get; private set; }
        public float RearLeftForwardSlip { get; private set; }
        public float RearLeftSidewaysSlip { get; private set; }
        public float RearRightForwardSlip { get; private set; }
        public float RearRightSidewaysSlip { get; private set; }
        public VehiclePerformanceStats CurrentPerformanceStats => _currentPerformanceStats;

        float IVehicleTelemetry.SpeedKph => VehicleTelemetryPresentation.ConvertSpeedToKph(CurrentForwardSpeed);
        int IVehicleTelemetry.CurrentGear => CurrentGear;
        float IVehicleTelemetry.EngineRpm => CurrentEngineRpm;
        bool IVehicleTelemetry.IsReversing => CurrentForwardSpeed < -ReversingDisplaySpeedThreshold;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _inputProvider = inputProviderComponent as IDrivingInputProvider;

            if (resetter == null)
            {
                resetter = GetComponent<VehicleResetter>();
            }

            ApplyConfiguration();
            _currentPerformanceStats = configuration != null
                ? configuration.CreatePerformanceStats()
                : default;
        }

        public void ConfigureRuntime(IDrivingInputProvider inputProvider, VehicleResetter vehicleResetter)
        {
            inputProviderComponent = inputProvider as MonoBehaviour;
            _inputProvider = inputProvider;
            resetter = vehicleResetter;
        }

        public void ApplyPerformanceStats(VehiclePerformanceStats performanceStats)
        {
            _currentPerformanceStats = performanceStats;
            _performanceStatsApplied = true;
            RefreshEffectiveTuning();
        }

        [ContextMenu("Copy Configuration To Runtime Tuning Overrides")]
        public void CopyConfigurationToRuntimeTuningOverrides()
        {
            if (configuration != null)
            {
                runtimeTuningOverrides = configuration.CreateTuning();
            }
        }

        private void FixedUpdate()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            RefreshEffectiveTuning();

            var input = _inputProvider != null ? _inputProvider.CurrentInput : DrivingInput.Neutral;
            if (input.ResetRequested)
            {
                TryResetVehicle();
                _inputProvider?.ConsumeResetRequest();
                return;
            }

            UpdateVehicleDiagnostics();
            UpdateForwardDriftState(input);
            ApplySteering(input.Steering);
            ApplyBrakes(input);
            ApplyRearSidewaysFriction();
            ApplyDriftAssist();
            var canSelectGear = AdvanceGearboxTiming();
            ApplyRearMotorTorque(input, canSelectGear);
            UpdateWheelSlipDiagnostics();
        }

        /// <summary>
        /// Uses the same reset boundary as the existing manual reset input and also
        /// clears controller-owned drivetrain state. Presentation interactions must
        /// call this rather than writing Rigidbody state themselves.
        /// </summary>
        public bool TryResetVehicle()
        {
            if (!HasRequiredReferences() || resetter == null || !resetter.TryResetVehicle())
            {
                return false;
            }

            ResetDrivetrain();
            UpdateVehicleDiagnostics();
            ApplyRearSidewaysFriction();
            return true;
        }

        private void LateUpdate()
        {
            SynchronizeWheelVisual(frontLeftWheel, frontLeftVisual);
            SynchronizeWheelVisual(frontRightWheel, frontRightVisual);
            SynchronizeWheelVisual(rearLeftWheel, rearLeftVisual);
            SynchronizeWheelVisual(rearRightWheel, rearRightVisual);
        }

        private void ApplyConfiguration()
        {
            if (_rigidbody == null || configuration == null)
            {
                return;
            }

            _rigidbody.useGravity = true;
            // Physics advances at the fixed timestep while the chase camera samples this
            // Rigidbody every rendered frame. Interpolate the rendered Rigidbody pose so
            // its CameraTarget child is continuous between physics steps.
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.ResetCenterOfMass();
            _rigidbody.ResetInertiaTensor();

            RefreshEffectiveTuning();
            _engineRpm = Mathf.Max(1f, _effectiveTuning.idleRpm);
        }

        private void RefreshEffectiveTuning()
        {
            if (configuration == null)
            {
                return;
            }

            _effectiveTuning = useRuntimeTuningOverrides
                ? runtimeTuningOverrides
                : configuration.CreateTuning();

            if (_performanceStatsApplied)
            {
                var baseline = configuration.CreatePerformanceStats();
                _effectiveTuning = VehiclePerformanceTuningRules.Apply(_effectiveTuning, baseline, _currentPerformanceStats);
                _performanceSteeringMultiplier = VehiclePerformanceTuningRules.RelativeMultiplier(baseline.Steering, _currentPerformanceStats.Steering);
            }

            if (_hasAppliedPhysicsTuning && _effectiveTuning.HasSamePhysicsSettings(_lastAppliedPhysicsTuning))
            {
                return;
            }

            ApplyPhysicsTuning(_effectiveTuning);
            _lastAppliedPhysicsTuning = _effectiveTuning;
            _hasAppliedPhysicsTuning = true;
        }

        private void ApplyPhysicsTuning(WheelVehicleTuning tuning)
        {
            if (_rigidbody == null)
            {
                return;
            }

            _rigidbody.mass = Mathf.Max(1f, tuning.mass);
            _rigidbody.linearDamping = Mathf.Max(0f, tuning.linearDamping);
            _rigidbody.angularDamping = Mathf.Max(0f, tuning.angularDamping);

            ConfigureWheel(frontLeftWheel, true, tuning);
            ConfigureWheel(frontRightWheel, true, tuning);
            ConfigureWheel(rearLeftWheel, false, tuning);
            ConfigureWheel(rearRightWheel, false, tuning);
            ConfigureLowSpeedSubsteps();
            _appliedRearSidewaysFrictionStiffness = float.NaN;
        }

        /// <summary>
        /// WheelColliders simulate poorly near standstill: undriven wheels stop turning and are dragged,
        /// and a car pulling away from a slow corner barely moves. More physics substeps below the
        /// threshold keep wheel and tyre contact stable at low speed. The setting is per vehicle, so it is
        /// applied through any one of its wheels.
        /// </summary>
        private void ConfigureLowSpeedSubsteps()
        {
            var wheel = frontLeftWheel != null ? frontLeftWheel : rearLeftWheel;
            if (wheel == null)
            {
                return;
            }

            wheel.ConfigureVehicleSubsteps(
                Mathf.Max(0f, lowSpeedSubstepThreshold),
                Mathf.Max(1, substepsBelowThreshold),
                Mathf.Max(1, substepsAboveThreshold));
        }

        private static void ConfigureWheel(WheelCollider wheel, bool isFrontWheel, WheelVehicleTuning tuning)
        {
            if (wheel == null)
            {
                return;
            }

            wheel.radius = Mathf.Max(0.01f, tuning.wheelRadius);
            wheel.mass = Mathf.Max(0.01f, tuning.wheelMass);
            wheel.wheelDampingRate = Mathf.Max(0f, tuning.wheelDampingRate);
            wheel.suspensionDistance = Mathf.Max(0f, tuning.suspensionDistance);
            wheel.forceAppPointDistance = Mathf.Max(0f, tuning.forceAppPointDistance);
            wheel.suspensionSpring = new JointSpring
            {
                spring = Mathf.Max(0f, tuning.suspensionSpring),
                damper = Mathf.Max(0f, tuning.suspensionDamper),
                targetPosition = Mathf.Clamp01(tuning.suspensionTargetPosition)
            };
            wheel.forwardFriction = (isFrontWheel ? tuning.frontForwardFriction : tuning.rearForwardFriction).CreateCurve();
            wheel.sidewaysFriction = (isFrontWheel ? tuning.frontSidewaysFriction : tuning.rearSidewaysFriction).CreateCurve();
        }

        private void UpdateVehicleDiagnostics()
        {
            var planarVelocity = Vector3.ProjectOnPlane(_rigidbody.linearVelocity, Vector3.up);
            CurrentForwardSpeed = Vector3.Dot(planarVelocity, transform.forward);
            CurrentLateralSpeed = Vector3.Dot(planarVelocity, transform.right);
            CurrentPlanarSpeed = planarVelocity.magnitude;
            CurrentVehicleSlipAngle = planarVelocity.sqrMagnitude > 0.01f
                ? Vector3.SignedAngle(transform.forward, planarVelocity.normalized, Vector3.up)
                : 0f;
            RearLeftWheelRpm = rearLeftWheel.rpm;
            RearRightWheelRpm = rearRightWheel.rpm;
            CurrentAverageDrivenWheelRpm = CalculateAverageDrivenWheelRpm();
            CurrentGearRatio = _effectiveTuning.GetGearRatio(_currentGearIndex);
            CurrentRawCalculatedEngineRpm = CalculateRawEngineRpm(CurrentGearRatio);
            CurrentSteeringSpeed = rearRightWheel.rpm * rearRightWheel.radius * Mathf.PI * 2f
                / Mathf.Max(0.001f, _effectiveTuning.steeringSpeedScale);
        }

        private void ApplySteering(float steeringInput)
        {
            CurrentSteeringInput = Mathf.Clamp(steeringInput, -1f, 1f);
            var baseSteer = CurrentSteeringInput * _effectiveTuning.steeringCurve.Evaluate(CurrentSteeringSpeed)
                * _performanceSteeringMultiplier;
            CurrentCountersteerCorrection = CalculateCountersteerCorrection();
            CurrentFrontWheelSteerAngle = Mathf.Clamp(
                baseSteer + CurrentCountersteerCorrection,
                -Mathf.Max(0f, _effectiveTuning.maximumSteeringAngle),
                Mathf.Max(0f, _effectiveTuning.maximumSteeringAngle));
            frontLeftWheel.steerAngle = CurrentFrontWheelSteerAngle;
            frontRightWheel.steerAngle = CurrentFrontWheelSteerAngle;
        }

        private float CalculateCountersteerCorrection()
        {
            if (!_effectiveTuning.countersteerEnabled)
            {
                return 0f;
            }

            var velocity = _rigidbody.linearVelocity;
            var guardAngle = Vector3.Angle(transform.forward, velocity - transform.forward);
            if (guardAngle >= Mathf.Clamp(_effectiveTuning.countersteerGuardAngle, 0f, 180f))
            {
                return 0f;
            }

            return Vector3.SignedAngle(transform.forward, velocity + transform.forward, Vector3.up)
                * Mathf.Max(0f, _effectiveTuning.countersteerStrength);
        }

        private void ApplyBrakes(DrivingInput input)
        {
            var normalBrakeInput = ResolveNormalBrakeInput(input);
            CurrentNormalBrakeTorque = normalBrakeInput * Mathf.Max(0f, _effectiveTuning.brakePower);
            HandbrakeActive = input.Drift;
            CurrentDriftHandbrakeMultiplier = GetDriftHandbrakeMultiplier();
            CurrentHandbrakeTorque = HandbrakeActive
                ? Mathf.Max(0f, _effectiveTuning.handbrakeTorque) * CurrentDriftHandbrakeMultiplier
                : 0f;

            var frontBrakeTorque = CurrentNormalBrakeTorque * Mathf.Max(0f, _effectiveTuning.frontBrakeFactor);
            var rearBrakeTorque = (CurrentNormalBrakeTorque * Mathf.Max(0f, _effectiveTuning.rearBrakeFactor)) + CurrentHandbrakeTorque;
            frontLeftWheel.brakeTorque = frontBrakeTorque;
            frontRightWheel.brakeTorque = frontBrakeTorque;
            rearLeftWheel.brakeTorque = rearBrakeTorque;
            rearRightWheel.brakeTorque = rearBrakeTorque;
        }

        private void ApplyRearMotorTorque(DrivingInput input, bool canSelectGear)
        {
            var motorInput = ResolveMotorInput(input);
            if (_performanceStatsApplied && motorInput > 0f)
            {
                motorInput *= VehiclePerformanceTuningRules.ForwardMotorFactor(CurrentForwardSpeed, _currentPerformanceStats.TopSpeed);
            }
            if (HandbrakeActive)
            {
                var torqueMultiplier = ForwardDriftActive
                    ? Mathf.Max(0f, _effectiveTuning.driftMotorTorqueMultiplier)
                    : Mathf.Clamp01(_effectiveTuning.handbrakeMotorTorqueMultiplier);
                motorInput *= torqueMultiplier;
            }

            if (Mathf.Abs(motorInput) < 0.001f)
            {
                CurrentMotorTorque = 0f;
                CurrentTorqueBeforeShiftSuppression = 0f;
                CurrentPowerCurveEvaluation = 0f;
                rearLeftWheel.motorTorque = 0f;
                rearRightWheel.motorTorque = 0f;
                UpdateCoastingGear(canSelectGear);
                UpdateIdleRpm();
                return;
            }

            UpdateAutomaticGearbox(motorInput > 0f, canSelectGear);
            var torqueMagnitude = CalculateRearWheelTorque();
            CurrentTorqueBeforeShiftSuppression = torqueMagnitude;
            if (_shiftTimeRemaining > 0f)
            {
                CurrentMotorTorque = 0f;
                rearLeftWheel.motorTorque = 0f;
                rearRightWheel.motorTorque = 0f;
                return;
            }

            CurrentMotorTorque = torqueMagnitude * motorInput * UpdateTractionControl(motorInput > 0f);
            rearLeftWheel.motorTorque = CurrentMotorTorque;
            rearRightWheel.motorTorque = CurrentMotorTorque;
        }

        // Traction control: wheelspin wastes power (and used to read as speed to the gearbox), so forward drive
        // torque is trimmed while the rear wheels outrun the car. Drifts keep their deliberate rear wheelspin.
        private const float TractionSlipAllowanceRatio = 0.2f;
        private const float TractionMinimumSlipAllowance = 2f;
        private const float TractionMinimumTorqueFactor = 0.15f;
        private const float TractionRecoveryPerSecond = 2.5f;

        private float UpdateTractionControl(bool drivingForward)
        {
            if (!drivingForward || HandbrakeActive || ForwardDriftActive)
            {
                _tractionFactor = 1f;
                return 1f;
            }

            var surfaceSpeed = CurrentAverageDrivenWheelRpm / 60f * 2f * Mathf.PI * rearLeftWheel.radius;
            var target = VehicleDrivetrainRules.EvaluateTractionControl(
                surfaceSpeed,
                CurrentForwardSpeed,
                TractionSlipAllowanceRatio,
                TractionMinimumSlipAllowance,
                TractionMinimumTorqueFactor);
            // Cut immediately, give torque back gradually so the wheels do not flare up again at once.
            _tractionFactor = target < _tractionFactor
                ? target
                : Mathf.MoveTowards(_tractionFactor, target, TractionRecoveryPerSecond * Time.fixedDeltaTime);
            return _tractionFactor;
        }

        private float CalculateRearWheelTorque()
        {
            var gearRatio = _effectiveTuning.GetGearRatio(_currentGearIndex);
            var normalizedEngineRpm = VehicleDrivetrainRules.NormalizeEngineRpm(_engineRpm, _effectiveTuning.redlineRpm);
            CurrentPowerCurveEvaluation = _effectiveTuning.enginePowerCurve != null
                ? Mathf.Max(0f, _effectiveTuning.enginePowerCurve.Evaluate(normalizedEngineRpm))
                : 1f;
            return VehicleDrivetrainRules.CalculateRearWheelTorque(
                CurrentPowerCurveEvaluation,
                _effectiveTuning.motorPower,
                _engineRpm,
                gearRatio,
                _effectiveTuning.differential);
        }

        private bool AdvanceGearboxTiming()
        {
            var timing = VehicleDrivetrainRules.EvaluateGearboxTiming(
                _shiftTimeRemaining,
                _shiftCooldownRemaining,
                Time.fixedDeltaTime,
                _effectiveTuning.gearShiftCooldownDuration);
            _shiftTimeRemaining = timing.ShiftTimeRemaining;
            _shiftCooldownRemaining = timing.ShiftCooldownRemaining;
            return timing.CanEvaluateGearSelection;
        }

        private void UpdateAutomaticGearbox(bool drivingForward, bool canSelectGear)
        {
            if (!canSelectGear)
            {
                return;
            }

            ClampCurrentGearIndex();
            CurrentRequestedGear = _currentGearIndex + 1;
            var gearRatio = _effectiveTuning.GetGearRatio(_currentGearIndex);
            CurrentGearRatio = gearRatio;
            CurrentAverageDrivenWheelRpm = CalculateAverageDrivenWheelRpm();
            CurrentRawCalculatedEngineRpm = CalculateRawEngineRpm(gearRatio);
            var response = Mathf.Max(0f, _effectiveTuning.engineRpmResponse);
            var followAmount = 1f - Mathf.Exp(-response * Time.fixedDeltaTime);
            var rpm = VehicleDrivetrainRules.EvaluateEngineRpm(
                _engineRpm,
                CurrentRawCalculatedEngineRpm,
                _effectiveTuning.idleRpm,
                _effectiveTuning.redlineRpm,
                followAmount);
            _engineRpm = rpm.EngineRpm;

            // Shift on road speed, not wheelspin: the engine still revs with spinning wheels, but a launch
            // must not climb through the gears before the car is actually moving.
            var shiftWheelRpm = VehicleDrivetrainRules.CalculateShiftWheelRpm(
                CurrentAverageDrivenWheelRpm,
                VehicleDrivetrainRules.CalculateGroundWheelRpm(CurrentForwardSpeed, rearLeftWheel.radius));
            CurrentShiftEngineRpm = VehicleDrivetrainRules.CalculateRawEngineRpm(shiftWheelRpm, gearRatio, _effectiveTuning.differential);
            var shiftDecision = VehicleDrivetrainRules.EvaluateAutomaticGearShift(
                drivingForward,
                _currentGearIndex,
                _effectiveTuning.gearRatios != null ? _effectiveTuning.gearRatios.Length : 0,
                Mathf.Min(rpm.TargetEngineRpm, CurrentShiftEngineRpm),
                _effectiveTuning.upshiftRpm,
                _effectiveTuning.downshiftRpm);
            if (!shiftDecision.ShouldShift)
            {
                return;
            }

            _currentGearIndex = shiftDecision.RequestedGearIndex;
            CurrentRequestedGear = shiftDecision.RequestedGearIndex + 1;
            _shiftTimeRemaining = Mathf.Max(0f, _effectiveTuning.gearShiftDuration);
        }

        private void UpdateCoastingGear(bool canSelectGear)
        {
            if (!canSelectGear)
            {
                return;
            }

            ClampCurrentGearIndex();
            var shiftDecision = VehicleDrivetrainRules.EvaluateCoastingDownshift(
                _currentGearIndex,
                CurrentForwardSpeed,
                CalculateRawEngineRpm(_effectiveTuning.GetGearRatio(_currentGearIndex)),
                _effectiveTuning.downshiftRpm);
            if (!shiftDecision.ShouldShift)
            {
                return;
            }

            _currentGearIndex = shiftDecision.RequestedGearIndex;
            CurrentRequestedGear = shiftDecision.RequestedGearIndex + 1;
            _shiftTimeRemaining = Mathf.Max(0f, _effectiveTuning.gearShiftDuration);
        }

        private void UpdateIdleRpm()
        {
            var response = Mathf.Max(0f, _effectiveTuning.engineRpmResponse);
            var followAmount = 1f - Mathf.Exp(-response * Time.fixedDeltaTime);
            _engineRpm = VehicleDrivetrainRules.EvaluateIdleEngineRpm(_engineRpm, _effectiveTuning.idleRpm, followAmount);
        }

        private float CalculateAverageDrivenWheelRpm()
        {
            return VehicleDrivetrainRules.CalculateAverageDrivenWheelRpm(rearLeftWheel.rpm, rearRightWheel.rpm);
        }

        private float CalculateRawEngineRpm(float gearRatio)
        {
            return VehicleDrivetrainRules.CalculateRawEngineRpm(
                CalculateAverageDrivenWheelRpm(),
                gearRatio,
                _effectiveTuning.differential);
        }

        private void ClampCurrentGearIndex()
        {
            var gearCount = _effectiveTuning.gearRatios != null ? _effectiveTuning.gearRatios.Length : 0;
            _currentGearIndex = VehicleDrivetrainRules.ClampGearIndex(_currentGearIndex, gearCount);
        }

        private void ResetDrivetrain()
        {
            _currentGearIndex = 0;
            _shiftTimeRemaining = 0f;
            _shiftCooldownRemaining = 0f;
            CurrentRequestedGear = 1;
            _engineRpm = Mathf.Max(1f, _effectiveTuning.idleRpm);
            CurrentMotorTorque = 0f;
            CurrentTorqueBeforeShiftSuppression = 0f;
            CurrentPowerCurveEvaluation = 0f;
            _tractionFactor = 1f;
            ForwardDriftActive = false;
            _wasForwardDriftActive = false;
            _driftAssistSessionRemaining = 0f;
            CurrentDriftAssistAcceleration = 0f;
            _driftHandbrakeEntryTimeRemaining = 0f;
            HandbrakeActive = false;
            CurrentNormalBrakeTorque = 0f;
            CurrentHandbrakeTorque = 0f;
            CurrentDriftHandbrakeMultiplier = 0f;
            CurrentSteeringInput = 0f;
            CurrentFrontWheelSteerAngle = 0f;
            CurrentCountersteerCorrection = 0f;
            ClearWheelForces(frontLeftWheel);
            ClearWheelForces(frontRightWheel);
            ClearWheelForces(rearLeftWheel);
            ClearWheelForces(rearRightWheel);
        }

        private static void ClearWheelForces(WheelCollider wheel)
        {
            if (wheel == null) return;
            wheel.motorTorque = 0f;
            wheel.brakeTorque = 0f;
            wheel.steerAngle = 0f;
        }

        private void UpdateForwardDriftState(DrivingInput input)
        {
            var reverseEntrySpeed = Mathf.Max(0f, _effectiveTuning.reverseEntrySpeed);
            ForwardDriftActive = input.Drift
                && input.Throttle > 0f
                && CurrentForwardSpeed >= -reverseEntrySpeed;

            if (ForwardDriftActive && !_wasForwardDriftActive)
            {
                _driftHandbrakeEntryTimeRemaining = Mathf.Max(0f, _effectiveTuning.driftHandbrakeEntryDuration);
            }

            if (!ForwardDriftActive)
            {
                _driftHandbrakeEntryTimeRemaining = 0f;
            }

            _wasForwardDriftActive = ForwardDriftActive;
        }

        private float GetDriftHandbrakeMultiplier()
        {
            if (!HandbrakeActive || !ForwardDriftActive)
            {
                return HandbrakeActive ? 1f : 0f;
            }

            if (_driftHandbrakeEntryTimeRemaining > 0f)
            {
                _driftHandbrakeEntryTimeRemaining = Mathf.Max(0f, _driftHandbrakeEntryTimeRemaining - Time.fixedDeltaTime);
                return 1f;
            }

            return Mathf.Max(0f, _effectiveTuning.driftHandbrakeHoldMultiplier);
        }

        private void ApplyDriftAssist()
        {
            CurrentDriftAssistAcceleration = 0f;
            if (!_effectiveTuning.driftAssistEnabled)
            {
                _driftAssistSessionRemaining = 0f;
                return;
            }

            _driftAssistSessionRemaining = ForwardDriftActive
                ? Mathf.Max(0f, _effectiveTuning.driftAssistSessionDuration)
                : Mathf.Max(0f, _driftAssistSessionRemaining - Time.fixedDeltaTime);
            var velocity = Vector3.ProjectOnPlane(_rigidbody.linearVelocity, Vector3.up);
            var assisting = _driftAssistSessionRemaining > 0f || _effectiveTuning.driftAssistOnAnySlide;
            if (!assisting || velocity.sqrMagnitude < VehicleDriftAssistRules.MinimumSpeed * VehicleDriftAssistRules.MinimumSpeed)
            {
                return;
            }

            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            var slipAngle = Vector3.SignedAngle(velocity, forward, Vector3.up);
            CurrentDriftAssistAcceleration = VehicleDriftAssistRules.CalculateYawAcceleration(
                slipAngle,
                _rigidbody.angularVelocity.y,
                _effectiveTuning.driftAssistMaximumSlipAngle,
                _effectiveTuning.driftAssistSlipGain,
                _effectiveTuning.driftAssistYawDamping,
                _effectiveTuning.driftAssistMaximumAcceleration);
            if (!Mathf.Approximately(CurrentDriftAssistAcceleration, 0f))
            {
                _rigidbody.AddTorque(Vector3.up * CurrentDriftAssistAcceleration, ForceMode.Acceleration);
            }
        }

        private void ApplyRearSidewaysFriction()
        {
            var rearSidewaysFriction = _effectiveTuning.rearSidewaysFriction.CreateCurve();
            if (ForwardDriftActive)
            {
                rearSidewaysFriction.stiffness *= Mathf.Max(0f, _effectiveTuning.driftRearSidewaysGripMultiplier);
            }

            CurrentRearSidewaysFrictionStiffness = rearSidewaysFriction.stiffness;
            if (Mathf.Approximately(_appliedRearSidewaysFrictionStiffness, rearSidewaysFriction.stiffness))
            {
                return;
            }

            rearLeftWheel.sidewaysFriction = rearSidewaysFriction;
            rearRightWheel.sidewaysFriction = rearSidewaysFriction;
            _appliedRearSidewaysFrictionStiffness = rearSidewaysFriction.stiffness;
        }

        private float ResolveNormalBrakeInput(DrivingInput input)
        {
            return VehicleDriveInputRules.ResolveNormalBrakeInput(
                input.Throttle,
                input.Brake,
                CurrentForwardSpeed,
                _effectiveTuning.reverseEntrySpeed);
        }

        private float ResolveMotorInput(DrivingInput input)
        {
            return VehicleDriveInputRules.ResolveMotorInput(
                input.Throttle,
                input.Brake,
                CurrentForwardSpeed,
                _effectiveTuning.reverseEntrySpeed);
        }

        private void UpdateWheelSlipDiagnostics()
        {
            UpdateWheelSlip(frontLeftWheel, out var frontLeftForwardSlip, out var frontLeftSidewaysSlip);
            UpdateWheelSlip(frontRightWheel, out var frontRightForwardSlip, out var frontRightSidewaysSlip);
            UpdateWheelSlip(rearLeftWheel, out var rearLeftForwardSlip, out var rearLeftSidewaysSlip);
            UpdateWheelSlip(rearRightWheel, out var rearRightForwardSlip, out var rearRightSidewaysSlip);
            FrontLeftForwardSlip = frontLeftForwardSlip;
            FrontLeftSidewaysSlip = frontLeftSidewaysSlip;
            FrontRightForwardSlip = frontRightForwardSlip;
            FrontRightSidewaysSlip = frontRightSidewaysSlip;
            RearLeftForwardSlip = rearLeftForwardSlip;
            RearLeftSidewaysSlip = rearLeftSidewaysSlip;
            RearRightForwardSlip = rearRightForwardSlip;
            RearRightSidewaysSlip = rearRightSidewaysSlip;
        }

        private static void UpdateWheelSlip(WheelCollider wheel, out float forwardSlip, out float sidewaysSlip)
        {
            forwardSlip = 0f;
            sidewaysSlip = 0f;
            if (wheel != null && wheel.GetGroundHit(out var hit))
            {
                forwardSlip = hit.forwardSlip;
                sidewaysSlip = hit.sidewaysSlip;
            }
        }

        private static void SynchronizeWheelVisual(WheelCollider wheel, Transform visual)
        {
            if (wheel == null || visual == null)
            {
                return;
            }

            wheel.GetWorldPose(out var position, out var rotation);
            visual.SetPositionAndRotation(position, rotation);
        }

        private bool HasRequiredReferences()
        {
            return configuration != null
                && _rigidbody != null
                && frontLeftWheel != null
                && frontRightWheel != null
                && rearLeftWheel != null
                && rearRightWheel != null;
        }

        private void OnDrawGizmosSelected()
        {
            var origin = transform.position + Vector3.up * 0.3f;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + transform.forward * 2f);

            if (_rigidbody != null)
            {
                var planarVelocity = Vector3.ProjectOnPlane(_rigidbody.linearVelocity, Vector3.up);
                if (planarVelocity.sqrMagnitude > 0.01f)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(origin, origin + planarVelocity.normalized * 2f);
                }
            }

            DrawFrontWheelGizmo(frontLeftWheel);
            DrawFrontWheelGizmo(frontRightWheel);
        }

        private void DrawFrontWheelGizmo(WheelCollider wheel)
        {
            if (wheel == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            var wheelDirection = Quaternion.AngleAxis(wheel.steerAngle, transform.up) * transform.forward;
            Gizmos.DrawLine(wheel.transform.position, wheel.transform.position + wheelDirection * 0.75f);
        }
    }
}
