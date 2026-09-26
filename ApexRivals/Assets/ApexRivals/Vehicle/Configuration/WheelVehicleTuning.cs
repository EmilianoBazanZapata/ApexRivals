using System;
using UnityEngine;

namespace ApexRivals.Vehicle.Configuration
{
    [Serializable]
    public struct WheelVehicleTuning
    {
        [Header("Rigidbody")]
        [Tooltip("Mass applied to the experimental wheel vehicle Rigidbody in kilograms. Higher mass increases inertia and changes suspension load; lower mass makes the chassis react more quickly.")]
        [Min(1f)] public float mass;
        [Tooltip("Rigidbody linear damping. Higher values remove linear momentum faster; lower values preserve coasting momentum.")]
        [Min(0f)] public float linearDamping;
        [Tooltip("Rigidbody angular damping. Higher values resist pitch, roll, and yaw more strongly; lower values allow more physical body response.")]
        [Min(0f)] public float angularDamping;

        [Header("Engine / Gearbox")]
        [Tooltip("Base engine power used by the rear-wheel torque calculation. Higher values increase rear-wheel torque and acceleration, but may cause more wheelspin.")]
        [Min(0f)] public float motorPower;
        [Tooltip("Final torque multiplier applied after the current gear ratio. Higher values increase wheel torque and acceleration but shorten effective gearing.")]
        [Min(0f)] public float differential;
        [Tooltip("Automatic forward gear ratios used by the physics-only powertrain. Higher ratios provide more wheel torque at lower speeds; lower ratios provide taller gearing.")]
        public float[] gearRatios;
        [Tooltip("Engine RPM below which the powertrain idles. Higher values increase the minimum RPM used by the torque calculation; lower values let the engine settle lower.")]
        [Min(1f)] public float idleRpm;
        [Tooltip("Engine RPM used to normalize the power curve. Higher values extend the usable RPM range; lower values reach the curve limit sooner.")]
        [Min(1f)] public float redlineRpm;
        [Tooltip("RPM above which the automatic powertrain selects the next higher gear. Higher values hold lower gears longer; lower values upshift sooner.")]
        [Min(1f)] public float upshiftRpm;
        [Tooltip("RPM below which the automatic powertrain selects the next lower gear. Higher values downshift sooner; lower values stay in taller gears longer.")]
        [Min(1f)] public float downshiftRpm;
        [Tooltip("Seconds that rear motor torque is suppressed during an automatic gear change. Higher values make shifts more noticeable; lower values make them quicker.")]
        [Min(0f)] public float gearShiftDuration;
        [Tooltip("Seconds after an automatic gear change during which new shift decisions are blocked while motor torque remains available. Higher values reduce rapid gear hunting; lower values allow quicker consecutive shifts.")]
        [Min(0f)] public float gearShiftCooldownDuration;
        [Tooltip("How quickly simulated engine RPM follows driven-wheel RPM. Higher values react faster; lower values smooth torque changes.")]
        [Min(0f)] public float engineRpmResponse;
        [Tooltip("Normalized engine-RPM-to-power curve used by the torque calculation. The X axis is normalized engine RPM (0 to 1 at redline); higher Y values create more rear-wheel torque.")]
        public AnimationCurve enginePowerCurve;

        [Header("Brakes")]
        [Tooltip("Base normal brake torque. Higher values slow or lock wheels more aggressively; lower values lengthen braking. Measured in WheelCollider brake-torque units.")]
        [Min(0f)] public float brakePower;
        [Tooltip("Per-front-wheel fraction of base brake power during normal braking. Higher values bias braking toward the front axle; lower values reduce front contribution.")]
        [Min(0f)] public float frontBrakeFactor;
        [Tooltip("Per-rear-wheel fraction of base brake power during normal braking. Higher values bias braking toward the rear axle; lower values reduce rear contribution.")]
        [Min(0f)] public float rearBrakeFactor;
        [Tooltip("Forward-speed threshold below which brake input transitions to reverse motor torque. Measured in m/s. Higher values permit reverse sooner; lower values require the vehicle to slow further first.")]
        [Min(0f)] public float reverseEntrySpeed;

        [Header("Handbrake")]
        [Tooltip("Rear-only brake torque applied while DrivingInput.Drift is active. Higher values lock or slow the rear axle more aggressively and make drift initiation easier; lower values make it gentler.")]
        [Min(0f)] public float handbrakeTorque;
        [Tooltip("Legacy motor-torque multiplier retained for non-forward handbrake behavior. Forward arcade drift uses Drift Motor Torque Multiplier instead so reverse behavior remains unchanged.")]
        [Range(0f, 1f)] public float handbrakeMotorTorqueMultiplier;
        [Tooltip("Multiplier applied to rear-wheel motor torque while drift input is active during forward driving. 1 keeps full propulsion, 0 disables rear motor torque, and values above 1 increase powered-drift torque.")]
        [Min(0f)] public float driftMotorTorqueMultiplier;
        [Tooltip("Multiplier applied to rear-wheel sideways friction while drift input is active during forward driving. Lower values make the rear axle slide more easily. 1 preserves normal rear lateral grip.")]
        [Min(0f)] public float driftRearSidewaysGripMultiplier;
        [Tooltip("Seconds that full rear handbrake torque is retained after forward drift begins. Higher values make entry braking last longer; lower values transition to the hold torque sooner.")]
        [Min(0f)] public float driftHandbrakeEntryDuration;
        [Tooltip("Scales rear handbrake torque after the forward drift-entry duration. Lower values let rear wheels keep rotating while reduced lateral grip and engine torque sustain the drift; 1 keeps full handbrake torque held.")]
        [Min(0f)] public float driftHandbrakeHoldMultiplier;

        [Header("Steering")]
        [Tooltip("Evaluates maximum front-wheel steering angle from the reference speed value. The X axis is rear-wheel RPM × wheel radius × 2π ÷ Steering Speed Scale; higher Y values allow greater steering angle.")]
        public AnimationCurve steeringCurve;
        [Tooltip("Divisor used by the reference steering-speed calculation: rear-wheel RPM × wheel radius × 2π ÷ this value. Higher values produce a smaller curve input and retain more steering authority at the same wheel RPM.")]
        [Min(0.001f)] public float steeringSpeedScale;
        [Tooltip("Absolute clamp for the final front-wheel steering angle. Higher values allow more steering authority; lower values limit front-wheel angle. Measured in degrees.")]
        [Min(0f)] public float maximumSteeringAngle;

        [Header("Countersteer")]
        [Tooltip("Enables the reference-style velocity-aware correction added to front-wheel steering while a slide remains within the guard angle.")]
        public bool countersteerEnabled;
        [Tooltip("Unsigned reference-style slip guard below which velocity-aware front-wheel countersteer is added. Higher values allow correction during more extreme slides; lower values restrict it earlier. Measured in degrees.")]
        [Range(0f, 180f)] public float countersteerGuardAngle;
        [Tooltip("Scales the velocity-aware steering correction added to the front wheels while the vehicle is crossed relative to travel direction. 0 disables the correction; 1 reproduces the full configured reference correction.")]
        [Range(0f, 2f)] public float countersteerStrength;

        [Header("Wheel Setup")]
        [Tooltip("WheelCollider tire radius. Higher values increase the physical tire contact radius; lower values reduce it. Measured in meters.")]
        [Min(0.01f)] public float wheelRadius;
        [Tooltip("Mass assigned to each WheelCollider. Higher values increase unsprung wheel mass; lower values let wheel RPM react more quickly. Measured in kilograms.")]
        [Min(0.01f)] public float wheelMass;
        [Tooltip("WheelCollider rotational damping rate. Higher values resist rapid wheel-RPM changes; lower values let wheels spin up and slow down faster.")]
        [Min(0f)] public float wheelDampingRate;
        [Tooltip("Distance below the wheel center where suspension forces are applied. Higher values change chassis leverage; lower values apply force closer to the wheel center. Measured in meters.")]
        [Min(0f)] public float forceAppPointDistance;

        [Header("Suspension")]
        [Tooltip("Maximum vertical wheel suspension travel. Higher values permit more body movement; lower values make travel shorter. Measured in meters.")]
        [Min(0f)] public float suspensionDistance;
        [Tooltip("WheelCollider suspension spring force. Higher values make the chassis resist compression more strongly; lower values make it softer.")]
        [Min(0f)] public float suspensionSpring;
        [Tooltip("WheelCollider suspension damping force. Higher values reduce bounce and oscillation more strongly; lower values allow more movement.")]
        [Min(0f)] public float suspensionDamper;
        [Tooltip("Normalized suspension rest position from 0 to 1. Higher values hold the wheel farther down its travel; lower values hold it farther up.")]
        [Range(0f, 1f)] public float suspensionTargetPosition;

        [Header("Front Forward Friction")]
        [Tooltip("Front-wheel longitudinal WheelCollider friction curve. It controls front acceleration and braking grip.")]
        public WheelFrictionSettings frontForwardFriction;
        [Header("Front Sideways Friction")]
        [Tooltip("Front-wheel lateral WheelCollider friction curve. Higher grip retains steering authority; lower grip makes the front slide more easily.")]
        public WheelFrictionSettings frontSidewaysFriction;
        [Header("Rear Forward Friction")]
        [Tooltip("Rear-wheel longitudinal WheelCollider friction curve. It controls RWD acceleration, braking, and power-slide behavior.")]
        public WheelFrictionSettings rearForwardFriction;
        [Header("Rear Sideways Friction")]
        [Tooltip("Rear-wheel lateral WheelCollider friction curve. Lower grip makes rear breakaway easier; higher grip makes the rear more planted.")]
        public WheelFrictionSettings rearSidewaysFriction;

        public float GetGearRatio(int gearIndex)
        {
            if (gearRatios == null || gearRatios.Length == 0)
            {
                return 1f;
            }

            return Mathf.Max(0f, gearRatios[Mathf.Clamp(gearIndex, 0, gearRatios.Length - 1)]);
        }

        public bool HasSamePhysicsSettings(in WheelVehicleTuning other)
        {
            return mass == other.mass
                && linearDamping == other.linearDamping
                && angularDamping == other.angularDamping
                && wheelRadius == other.wheelRadius
                && wheelMass == other.wheelMass
                && wheelDampingRate == other.wheelDampingRate
                && suspensionDistance == other.suspensionDistance
                && forceAppPointDistance == other.forceAppPointDistance
                && suspensionSpring == other.suspensionSpring
                && suspensionDamper == other.suspensionDamper
                && suspensionTargetPosition == other.suspensionTargetPosition
                && HasSameFriction(frontForwardFriction, other.frontForwardFriction)
                && HasSameFriction(frontSidewaysFriction, other.frontSidewaysFriction)
                && HasSameFriction(rearForwardFriction, other.rearForwardFriction)
                && HasSameFriction(rearSidewaysFriction, other.rearSidewaysFriction);
        }

        private static bool HasSameFriction(WheelFrictionSettings first, WheelFrictionSettings second)
        {
            return first.extremumSlip == second.extremumSlip
                && first.extremumValue == second.extremumValue
                && first.asymptoteSlip == second.asymptoteSlip
                && first.asymptoteValue == second.asymptoteValue
                && first.stiffness == second.stiffness;
        }
    }
}
