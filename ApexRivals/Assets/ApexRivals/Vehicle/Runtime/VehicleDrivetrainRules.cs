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
