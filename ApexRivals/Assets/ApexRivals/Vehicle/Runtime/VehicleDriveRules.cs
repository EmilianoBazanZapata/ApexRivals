using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    public static class VehicleDriveRules
    {
        public static VehicleDriveMode ResolveDriveMode(float forwardSpeed, float throttleInput, float brakeInput, float reverseEntrySpeed)
        {
            if (throttleInput > 0f)
            {
                return forwardSpeed < -reverseEntrySpeed ? VehicleDriveMode.Brake : VehicleDriveMode.Forward;
            }

            if (brakeInput <= 0f)
            {
                return VehicleDriveMode.Coast;
            }

            return forwardSpeed > reverseEntrySpeed ? VehicleDriveMode.Brake : VehicleDriveMode.Reverse;
        }

        public static float CalculateNormalizedSpeed(float speed, float maximumSpeed)
        {
            if (maximumSpeed <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Abs(speed) / maximumSpeed);
        }

        public static float SelectLateralGrip(float normalGrip, float driftGrip, bool driftRequested)
        {
            return driftRequested ? driftGrip : normalGrip;
        }

        public static float CalculateSteeringResponse(float speed, float maximumSpeed, float minimumResponse)
        {
            var normalizedSpeed = CalculateNormalizedSpeed(speed, maximumSpeed);
            return Mathf.Lerp(Mathf.Clamp01(minimumResponse), 1f, normalizedSpeed);
        }
    }
}
