using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Vehicle.Configuration
{
    [CreateAssetMenu(fileName = "WheelVehicleConfiguration", menuName = "Apex Rivals/Vehicle/Wheel Vehicle Configuration")]
    public sealed class WheelVehicleConfiguration : ScriptableObject, IVehiclePerformanceStatsSource
    {
        [Header("Rigidbody")]
        [Tooltip("Mass applied to the experimental wheel vehicle Rigidbody in kilograms. Higher mass increases inertia and changes suspension load.")]
        [Min(1f)] public float mass = 1000f;
        [Tooltip("Rigidbody linear damping. Higher values remove linear momentum faster; zero preserves the reference baseline.")]
        [Min(0f)] public float linearDamping;
        [Tooltip("Rigidbody angular damping. Higher values resist pitch, roll, and yaw more strongly; the reference baseline is 0.05.")]
        [Min(0f)] public float angularDamping = 0.05f;

        [Header("Engine / RWD")]
        [Tooltip("Reference-style engine power value used by the rear-wheel torque calculation. Higher values increase rear-wheel drive torque.")]
        [Min(0f)] public float motorPower = 100f;
        [Tooltip("Final-drive multiplier applied after the selected gear ratio. Higher values increase wheel torque and engine RPM for a given wheel speed.")]
        [Min(0f)] public float differential = 4f;
        [Tooltip("Automatic forward gear ratios used by the physics-only powertrain. Higher ratios provide more wheel torque at lower speeds.")]
        public float[] gearRatios = { 3f, 2.5f, 2f, 1.5f, 1f, 0.8f };
        [Tooltip("Engine RPM below which the powertrain idles. Higher values increase the minimum RPM used by the torque calculation.")]
        [Min(1f)] public float idleRpm = 800f;
        [Tooltip("Engine RPM used to normalize the power curve. Higher values extend the usable RPM range.")]
        [Min(1f)] public float redlineRpm = 6500f;
        [Tooltip("RPM above which the automatic powertrain selects the next higher gear. Higher values hold lower gears longer.")]
        [Min(1f)] public float upshiftRpm = 5500f;
        [Tooltip("RPM below which the automatic powertrain selects the next lower gear. Higher values downshift sooner.")]
        [Min(1f)] public float downshiftRpm = 3300f;
        [Tooltip("Seconds that rear motor torque is suppressed during an automatic gear change. Higher values make shifts more noticeable.")]
        [Min(0f)] public float gearShiftDuration = 0.12f;
        [Tooltip("Seconds after an automatic gear change during which new shift decisions are blocked while motor torque remains available. Higher values reduce rapid gear hunting; lower values allow quicker consecutive shifts.")]
        [Min(0f)] public float gearShiftCooldownDuration = 0.25f;
        [Tooltip("How quickly simulated engine RPM follows driven-wheel RPM. Higher values react faster; lower values smooth torque changes.")]
        [Min(0f)] public float engineRpmResponse = 3f;
        [Tooltip("Normalized engine-RPM-to-power curve used by the reference-style torque calculation. Higher curve values create more rear-wheel torque at that RPM.")]
        public AnimationCurve enginePowerCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f),
            new Keyframe(1.1105323f, -0.011385194f));

        [Header("Brakes / Handbrake")]
        [Tooltip("Base brake torque shared by normal braking. Higher values slow or lock wheels more aggressively. Measured in WheelCollider brake-torque units.")]
        [Min(0f)] public float brakePower = 50000f;
        [Tooltip("Per-front-wheel fraction of base brake power during normal braking. Higher values make the front axle contribute more braking.")]
        [Min(0f)] public float frontBrakeFactor = 0.7f;
        [Tooltip("Per-rear-wheel fraction of base brake power during normal braking. Higher values make the rear axle contribute more braking.")]
        [Min(0f)] public float rearBrakeFactor = 0.3f;
        [Tooltip("Brake torque applied only to rear wheels while DrivingInput.Drift is active. Higher values lock or slow the rear wheels more aggressively and make rear breakaway easier.")]
        [Min(0f)] public float handbrakeTorque = 50000f;
        [Tooltip("Legacy motor-torque multiplier retained for non-forward handbrake behavior. Forward arcade drift uses Drift Motor Torque Multiplier instead so reverse behavior remains unchanged.")]
        [Range(0f, 1f)] public float handbrakeMotorTorqueMultiplier;
        [Tooltip("Multiplier applied to rear-wheel motor torque while drift input is active during forward driving. 1 keeps full propulsion, 0 disables rear motor torque, and values above 1 increase powered-drift torque.")]
        [Min(0f)] public float driftMotorTorqueMultiplier = 1f;
        [Tooltip("Multiplier applied to rear-wheel sideways friction while drift input is active during forward driving. Lower values make the rear axle slide more easily. 1 preserves normal rear lateral grip.")]
        [Min(0f)] public float driftRearSidewaysGripMultiplier = 0.5f;
        [Tooltip("Seconds that full rear handbrake torque is retained after forward drift begins. Higher values make entry braking last longer; lower values transition to the hold torque sooner.")]
        [Min(0f)] public float driftHandbrakeEntryDuration = 0.1f;
        [Tooltip("Scales rear handbrake torque after the forward drift-entry duration. Lower values let rear wheels keep rotating while reduced lateral grip and engine torque sustain the drift; 1 keeps full handbrake torque held.")]
        [Min(0f)] public float driftHandbrakeHoldMultiplier = 0.1f;
        [Tooltip("Forward-speed threshold below which brake input transitions to reverse motor torque. Measured in m/s. Higher values permit reverse sooner.")]
        [Min(0f)] public float reverseEntrySpeed = 0.5f;

        [Header("Steering / Countersteer")]
        [Tooltip("Steering angle selected from the reference-style wheel-RPM-derived speed value. Higher curve values increase front-wheel steering authority.")]
        public AnimationCurve steeringCurve = new AnimationCurve(
            new Keyframe(-0.9073868f, 63.596565f),
            new Keyframe(82.59024f, 15.46351f));
        [Tooltip("Divisor used by the reference steering-speed calculation: rear-wheel RPM × wheel radius × 2π ÷ this value. Higher values produce a smaller curve input and retain more steering authority at the same wheel RPM.")]
        [Min(0.001f)] public float steeringSpeedScale = 10f;
        [Tooltip("Absolute clamp for the final front-wheel steering angle. Higher values allow more steering authority. Measured in degrees.")]
        [Min(0f)] public float maximumSteeringAngle = 90f;
        [Tooltip("Enables the reference-style velocity-aware correction that is added to front-wheel steering while a slide remains within the guard angle.")]
        public bool countersteerEnabled = true;
        [Tooltip("Unsigned reference-style slip guard below which velocity-aware front-wheel countersteer is added. Higher values allow correction during more extreme slides. Measured in degrees.")]
        [Range(0f, 180f)] public float countersteerGuardAngle = 120f;
        [Tooltip("Scales the velocity-aware steering correction added to the front wheels while the vehicle is crossed relative to travel direction. 0 disables the correction; 1 reproduces the full configured reference correction.")]
        [Range(0f, 2f)] public float countersteerStrength = 1f;

        [Header("Wheel Setup / Suspension")]
        [Tooltip("WheelCollider radius and matching wheel-visual radius. Higher values increase the tire contact radius. Measured in meters.")]
        [Min(0.01f)] public float wheelRadius = 0.3f;
        [Tooltip("Mass assigned to each WheelCollider. Higher values increase unsprung wheel mass. Measured in kilograms.")]
        [Min(0.01f)] public float wheelMass = 20f;
        [Tooltip("WheelCollider rotational damping rate. Higher values resist rapid wheel-RPM changes.")]
        [Min(0f)] public float wheelDampingRate = 0.25f;
        [Tooltip("Maximum vertical wheel suspension travel. Higher values permit more body movement. Measured in meters.")]
        [Min(0f)] public float suspensionDistance = 0.1f;
        [Tooltip("Distance below the wheel center where suspension forces are applied. Higher values change chassis leverage. Measured in meters.")]
        [Min(0f)] public float forceAppPointDistance;
        [Tooltip("Suspension spring force. Higher values make the chassis resist compression more strongly.")]
        [Min(0f)] public float suspensionSpring = 50000f;
        [Tooltip("Suspension damping force. Higher values reduce bounce more strongly.")]
        [Min(0f)] public float suspensionDamper = 4500f;
        [Tooltip("Normalized suspension rest position from 0 to 1. Higher values hold the wheel farther down its travel.")]
        [Range(0f, 1f)] public float suspensionTargetPosition = 0.5f;

        [Header("Front Friction")]
        [Tooltip("Front-wheel longitudinal friction. This affects acceleration and braking grip at the steering axle.")]
        public WheelFrictionSettings frontForwardFriction = new WheelFrictionSettings { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1f };
        [Tooltip("Front-wheel lateral friction. The stronger reference front axle preserves steering authority during a slide.")]
        public WheelFrictionSettings frontSidewaysFriction = new WheelFrictionSettings { extremumSlip = 0.2f, extremumValue = 1f, asymptoteSlip = 0.5f, asymptoteValue = 0.75f, stiffness = 1f };

        [Header("Rear Friction")]
        [Tooltip("Rear-wheel longitudinal friction. This affects RWD acceleration, braking, and power-slide behavior.")]
        public WheelFrictionSettings rearForwardFriction = new WheelFrictionSettings { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1f };
        [Tooltip("Rear-wheel lateral friction. The weaker reference rear axle breaks away before the front axle during a drift.")]
        public WheelFrictionSettings rearSidewaysFriction = new WheelFrictionSettings { extremumSlip = 0.1f, extremumValue = 0.7f, asymptoteSlip = 0.1f, asymptoteValue = 0.6f, stiffness = 0.8f };

        [Header("Selection / Progression")]
        [Tooltip("Game-facing acceleration rating for selection and upgrades. This value does not change WheelCollider physics directly.")]
        [Min(0f)] public float performanceAcceleration;
        [Tooltip("Game-facing top-speed rating for selection and upgrades. This value does not change WheelCollider physics directly.")]
        [Min(0f)] public float performanceTopSpeed;
        [Tooltip("Game-facing steering rating for selection and upgrades. This value does not change WheelCollider physics directly.")]
        [Min(0f)] public float performanceSteering;
        [Tooltip("Game-facing normal-handling rating for selection and upgrades. This value does not change WheelCollider friction directly.")]
        [Min(0f)] public float performanceHandling;
        [Tooltip("Game-facing drift-handling rating for selection and upgrades. This value does not change WheelCollider friction directly.")]
        [Min(0f)] public float performanceDriftHandling;

        public float GetGearRatio(int gearIndex)
        {
            if (gearRatios == null || gearRatios.Length == 0)
            {
                return 1f;
            }

            return Mathf.Max(0f, gearRatios[Mathf.Clamp(gearIndex, 0, gearRatios.Length - 1)]);
        }

        public WheelVehicleTuning CreateTuning()
        {
            return new WheelVehicleTuning
            {
                mass = mass,
                linearDamping = linearDamping,
                angularDamping = angularDamping,
                motorPower = motorPower,
                differential = differential,
                gearRatios = gearRatios,
                idleRpm = idleRpm,
                redlineRpm = redlineRpm,
                upshiftRpm = upshiftRpm,
                downshiftRpm = downshiftRpm,
                gearShiftDuration = gearShiftDuration,
                gearShiftCooldownDuration = gearShiftCooldownDuration,
                engineRpmResponse = engineRpmResponse,
                enginePowerCurve = enginePowerCurve,
                brakePower = brakePower,
                frontBrakeFactor = frontBrakeFactor,
                rearBrakeFactor = rearBrakeFactor,
                reverseEntrySpeed = reverseEntrySpeed,
                handbrakeTorque = handbrakeTorque,
                handbrakeMotorTorqueMultiplier = handbrakeMotorTorqueMultiplier,
                driftMotorTorqueMultiplier = driftMotorTorqueMultiplier,
                driftRearSidewaysGripMultiplier = driftRearSidewaysGripMultiplier,
                driftHandbrakeEntryDuration = driftHandbrakeEntryDuration,
                driftHandbrakeHoldMultiplier = driftHandbrakeHoldMultiplier,
                steeringCurve = steeringCurve,
                steeringSpeedScale = steeringSpeedScale,
                maximumSteeringAngle = maximumSteeringAngle,
                countersteerEnabled = countersteerEnabled,
                countersteerGuardAngle = countersteerGuardAngle,
                countersteerStrength = countersteerStrength,
                wheelRadius = wheelRadius,
                wheelMass = wheelMass,
                wheelDampingRate = wheelDampingRate,
                forceAppPointDistance = forceAppPointDistance,
                suspensionDistance = suspensionDistance,
                suspensionSpring = suspensionSpring,
                suspensionDamper = suspensionDamper,
                suspensionTargetPosition = suspensionTargetPosition,
                frontForwardFriction = frontForwardFriction,
                frontSidewaysFriction = frontSidewaysFriction,
                rearForwardFriction = rearForwardFriction,
                rearSidewaysFriction = rearSidewaysFriction
            };
        }

        public VehiclePerformanceStats CreatePerformanceStats()
        {
            return new VehiclePerformanceStats(
                performanceAcceleration,
                performanceTopSpeed,
                performanceSteering,
                performanceHandling,
                performanceDriftHandling);
        }
    }
}
