namespace ApexRivals.Vehicle.Runtime
{
    public static class VehicleDriveInputRules
    {
        public static float ResolveNormalBrakeInput(
            float throttleInput,
            float brakeInput,
            float signedForwardSpeed,
            float reverseEntrySpeed)
        {
            var threshold = reverseEntrySpeed > 0f ? reverseEntrySpeed : 0f;
            if (throttleInput > 0f && signedForwardSpeed < -threshold)
            {
                return throttleInput;
            }

            return brakeInput > 0f && signedForwardSpeed > threshold ? brakeInput : 0f;
        }

        public static float ResolveMotorInput(
            float throttleInput,
            float brakeInput,
            float signedForwardSpeed,
            float reverseEntrySpeed)
        {
            var threshold = reverseEntrySpeed > 0f ? reverseEntrySpeed : 0f;
            if (throttleInput > 0f && signedForwardSpeed >= -threshold)
            {
                return throttleInput;
            }

            if (brakeInput > 0f && signedForwardSpeed <= threshold)
            {
                return -brakeInput;
            }

            return 0f;
        }
    }
}
