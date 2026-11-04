using System;

namespace ApexRivals.Vehicle.Runtime
{
    public static class VehicleDrivetrainRules
    {
        public static float CalculateAverageDrivenWheelRpm(float rearLeftWheelRpm, float rearRightWheelRpm)
        {
            return (MathF.Abs(rearLeftWheelRpm) + MathF.Abs(rearRightWheelRpm)) * 0.5f;
        }

        public static float CalculateRawEngineRpm(float averageDrivenWheelRpm, float gearRatio, float differential)
        {
            return averageDrivenWheelRpm * MathF.Abs(gearRatio) * MathF.Abs(differential);
        }

        /// <summary>Wheel RPM that matches the car's speed over the ground, i.e. with no wheelspin.</summary>
        public static float CalculateGroundWheelRpm(float forwardSpeed, float wheelRadius)
        {
            return MathF.Abs(forwardSpeed) / (2f * MathF.PI * Max(0.01f, wheelRadius)) * 60f;
        }

        /// <summary>
        /// Wheel RPM the automatic gearbox shifts on: the driven wheels, but never faster than the car actually
        /// moves. Wheelspin at launch would otherwise read as speed and run up through the gears in place.
        /// </summary>
        public static float CalculateShiftWheelRpm(float averageDrivenWheelRpm, float groundWheelRpm)
        {
            return MathF.Min(MathF.Abs(averageDrivenWheelRpm), MathF.Abs(groundWheelRpm));
        }

        /// <summary>
        /// Arcade traction control: the share of drive torque to keep when the driven wheels' surface speed runs
        /// ahead of the car by more than <paramref name="allowanceRatio"/> of its speed plus
        /// <paramref name="minimumAllowance"/> (m/s). Never drops below <paramref name="minimumFactor"/>.
        /// </summary>
        public static float EvaluateTractionControl(
            float drivenWheelSurfaceSpeed,
            float groundSpeed,
            float allowanceRatio,
            float minimumAllowance,
            float minimumFactor)
        {
            var allowed = MathF.Abs(groundSpeed) * (1f + Max(0f, allowanceRatio)) + Max(0f, minimumAllowance);
            var surface = MathF.Abs(drivenWheelSurfaceSpeed);
            if (surface <= allowed)
            {
                return 1f;
            }

            return Clamp(allowed / surface, Clamp(minimumFactor, 0f, 1f), 1f);
        }

        public static float NormalizeEngineRpm(float engineRpm, float redlineRpm)
        {
            return Clamp(engineRpm / Max(1f, redlineRpm), 0f, 1f);
        }

        public static float CalculateRearWheelTorque(
            float powerCurveEvaluation,
            float motorPower,
            float engineRpm,
            float gearRatio,
            float differential)
        {
            return powerCurveEvaluation
                * Max(0f, motorPower)
                / Max(1f, engineRpm)
                * gearRatio
                * Max(0f, differential)
                * 5252f;
        }

        public static EngineRpmEvaluation EvaluateEngineRpm(
            float currentEngineRpm,
            float rawEngineRpm,
            float idleRpm,
            float redlineRpm,
            float followAmount)
        {
            var minimumEngineRpm = Max(1f, idleRpm);
            var maximumEngineRpm = Max(minimumEngineRpm, redlineRpm);
            var targetEngineRpm = Clamp(rawEngineRpm, minimumEngineRpm, maximumEngineRpm);
            var engineRpm = Lerp(
                Clamp(currentEngineRpm, minimumEngineRpm, maximumEngineRpm),
                targetEngineRpm,
                followAmount);
            return new EngineRpmEvaluation(targetEngineRpm, engineRpm);
        }

        /// <summary>
        /// Engine RPM to show and play (not to drive with). Off the throttle the engine follows the road (engine
        /// braking). On it, the driven wheels may rev it up to <paramref name="wheelspinFlareRpm"/> past road speed,
        /// or to <paramref name="launchFlareRpm"/> above idle when launching, so a slow drift does not read as the redline.
        /// </summary>
        public static float EvaluatePresentedEngineRpm(
            float drivenWheelEngineRpm,
            float roadEngineRpm,
            bool onThrottle,
            float idleRpm,
            float redlineRpm,
            float wheelspinFlareRpm,
            float launchFlareRpm)
        {
            var minimumEngineRpm = Max(1f, idleRpm);
            var maximumEngineRpm = Max(minimumEngineRpm, redlineRpm);
            var road = MathF.Abs(roadEngineRpm);
            if (!onThrottle)
            {
                return Clamp(road, minimumEngineRpm, maximumEngineRpm);
            }

            var limit = Max(road + Max(0f, wheelspinFlareRpm), minimumEngineRpm + Max(0f, launchFlareRpm));
            var engineRpm = MathF.Min(Max(MathF.Abs(drivenWheelEngineRpm), road * 0.9f), limit);
            return Clamp(engineRpm, minimumEngineRpm, maximumEngineRpm);
        }

        public static float EvaluateIdleEngineRpm(float currentEngineRpm, float idleRpm, float followAmount)
        {
            return Lerp(currentEngineRpm, Max(1f, idleRpm), followAmount);
        }

        public static GearboxTimingEvaluation EvaluateGearboxTiming(
            float shiftTimeRemaining,
            float shiftCooldownRemaining,
            float fixedDeltaTime,
            float cooldownDuration)
        {
            if (shiftTimeRemaining > 0f)
            {
                shiftTimeRemaining = Max(0f, shiftTimeRemaining - fixedDeltaTime);
                if (shiftTimeRemaining <= 0f)
                {
                    shiftCooldownRemaining = Max(0f, cooldownDuration);
                }

                return new GearboxTimingEvaluation(shiftTimeRemaining, shiftCooldownRemaining, false);
            }

            if (shiftCooldownRemaining > 0f)
            {
                shiftCooldownRemaining = Max(0f, shiftCooldownRemaining - fixedDeltaTime);
                return new GearboxTimingEvaluation(shiftTimeRemaining, shiftCooldownRemaining, false);
            }

            return new GearboxTimingEvaluation(shiftTimeRemaining, shiftCooldownRemaining, true);
        }

        public static int ClampGearIndex(int gearIndex, int gearCount)
        {
            return gearCount <= 0 ? 0 : Clamp(gearIndex, 0, gearCount - 1);
        }

        public static GearShiftDecision EvaluateAutomaticGearShift(
            bool drivingForward,
            int currentGearIndex,
            int gearCount,
            float targetEngineRpm,
            float upshiftRpm,
            float downshiftRpm)
        {
            if (!drivingForward || gearCount <= 0)
            {
                return new GearShiftDecision(currentGearIndex, false);
            }

            var requestedGearIndex = currentGearIndex;
            if (targetEngineRpm > upshiftRpm && currentGearIndex < gearCount - 1)
            {
                requestedGearIndex++;
            }
            else if (targetEngineRpm < downshiftRpm && currentGearIndex > 0)
            {
                requestedGearIndex--;
            }

            return new GearShiftDecision(requestedGearIndex, requestedGearIndex != currentGearIndex);
        }

        /// <summary>
        /// Downshift while coasting or braking: without power the gearbox would otherwise keep the gear it
        /// had at speed, and the car would pull away from a slow corner in a tall gear with little torque.
        /// Only shifts down, one gear per evaluation, and only while rolling forward.
        /// </summary>
        public static GearShiftDecision EvaluateCoastingDownshift(
            int currentGearIndex,
            float signedForwardSpeed,
            float rawEngineRpm,
            float downshiftRpm)
        {
            if (currentGearIndex <= 0 || signedForwardSpeed <= 0f || rawEngineRpm >= downshiftRpm)
            {
                return new GearShiftDecision(currentGearIndex, false);
            }

            return new GearShiftDecision(currentGearIndex - 1, true);
        }

        private static float Max(float first, float second)
        {
            return first > second ? first : second;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static float Lerp(float from, float to, float amount)
        {
            var clampedAmount = Clamp(amount, 0f, 1f);
            return from + ((to - from) * clampedAmount);
        }
    }
}
