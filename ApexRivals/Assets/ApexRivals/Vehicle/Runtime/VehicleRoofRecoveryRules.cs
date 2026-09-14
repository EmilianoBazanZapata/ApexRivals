namespace ApexRivals.Vehicle.Runtime
{
    public static class VehicleRoofRecoveryRules
    {
        public static bool IsInverted(float upDot, float invertedDotThreshold)
        {
            return upDot <= invertedDotThreshold;
        }

        public static bool CanRecover(bool isInverted, bool roofRayHit, float invertedTime, float debounceDuration)
        {
            return isInverted
                && roofRayHit
                && invertedTime >= debounceDuration;
        }

        public static bool ShouldRequestRecovery(bool canRecoverVehicle, RecoveryInputSource inputSource)
        {
            return canRecoverVehicle && inputSource != RecoveryInputSource.None;
        }
    }
}
