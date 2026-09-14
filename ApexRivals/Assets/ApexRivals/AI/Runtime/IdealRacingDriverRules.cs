using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// Pure, deterministic control helpers for the baseline racing driver.  These rules only
    /// produce normalized <see cref="DrivingInput"/> values; they never manipulate vehicle physics.
    /// </summary>
    public static class IdealRacingDriverRules
    {
        public static int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        public static int ForwardIndexDistance(int fromIndex, int toIndex, int count)
        {
            return WrapIndex(toIndex - fromIndex, count);
        }

        public static bool IsSequentialAdvance(int currentIndex, int candidateIndex, int maximumAdvance, int count)
        {
            if (count <= 0)
            {
                return false;
            }

            return ForwardIndexDistance(currentIndex, candidateIndex, count) <= Mathf.Max(0, maximumAdvance);
        }

        public static bool HasReachedPersistentTarget(float directDistance, float reachedRadius)
        {
            return directDistance <= Mathf.Max(0f, reachedRadius);
        }

        /// <summary>
        /// A target may only be treated as behind when local, trusted path progression has moved
        /// from that sample to the current sample.  This avoids false positives across hairpins.
        /// </summary>
        public static bool IsTargetAtOrBehindProgress(int currentIndex, int targetIndex, int maximumSequentialAdvance, int count)
        {
            if (count <= 0)
            {
                return false;
            }

            var progressSinceTarget = ForwardIndexDistance(targetIndex, currentIndex, count);
            return progressSinceTarget <= Mathf.Max(0, maximumSequentialAdvance);
        }

        public static bool HasPassedPersistentTarget(
            Vector3 vehiclePosition,
            Vector3 targetPosition,
            Vector3 targetForward,
            int currentIndex,
            int targetIndex,
            int maximumSequentialAdvance,
            int count)
        {
            if (!IsTargetAtOrBehindProgress(currentIndex, targetIndex, maximumSequentialAdvance, count))
            {
                return false;
            }

            var planarForward = Vector3.ProjectOnPlane(targetForward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            var toVehicle = Vector3.ProjectOnPlane(vehiclePosition - targetPosition, Vector3.up);
            return Vector3.Dot(toVehicle, planarForward.normalized) > 0f;
        }

        public static bool ShouldAdvancePersistentTarget(bool hasValidTarget, bool reachedTarget, bool passedTarget, bool targetBehindProgress)
        {
            return !hasValidTarget || reachedTarget || passedTarget || targetBehindProgress;
        }

        public static int ResolveResetAnchorIndex(int lastSafeIndex, int lastCheckpointIndex, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return WrapIndex(lastSafeIndex >= 0 ? lastSafeIndex : lastCheckpointIndex, count);
        }

        public static float CalculateSignedHeadingError(Vector3 vehicleForward, Vector3 pathForward)
        {
            var planarVehicleForward = Vector3.ProjectOnPlane(vehicleForward, Vector3.up);
            var planarPathForward = Vector3.ProjectOnPlane(pathForward, Vector3.up);
            if (planarVehicleForward.sqrMagnitude < 0.0001f || planarPathForward.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            // Positive means the path lies to the vehicle's right, which maps to positive steering.
            return Vector3.SignedAngle(planarVehicleForward, planarPathForward, Vector3.up);
        }

        public static float CalculateSteering(
            float signedHeadingErrorDegrees,
            float signedCrossTrackError,
            float speed,
            float headingGain,
            float crossTrackGain,
            float speedSoftening)
        {
            var headingCommand = signedHeadingErrorDegrees / 45f * Mathf.Max(0f, headingGain);
            var denominator = Mathf.Max(0.1f, speed + Mathf.Max(0.1f, speedSoftening));
            var crossTrackAngle = Mathf.Atan(Mathf.Max(0f, crossTrackGain) * signedCrossTrackError / denominator) * Mathf.Rad2Deg;

            // A vehicle to the path's right must steer left, hence the opposite sign.
            var crossTrackCommand = -crossTrackAngle / 45f;
            return Mathf.Clamp(headingCommand + crossTrackCommand, -1f, 1f);
        }

        public static float CalculateLookahead(float speed, float speedLimit, float curvature, float minimumDistance, float maximumDistance)
        {
            var minimum = Mathf.Max(0f, minimumDistance);
            var maximum = Mathf.Max(minimum, maximumDistance);
            var speedFraction = Mathf.Clamp01(speed / Mathf.Max(0.1f, speedLimit));
            var straightLineLookahead = Mathf.Lerp(minimum, maximum, speedFraction);
            var curvatureReduction = 1f / (1f + Mathf.Max(0f, curvature) * 8f);
            return Mathf.Clamp(Mathf.Lerp(minimum, straightLineLookahead, curvatureReduction), minimum, maximum);
        }

        public static float CalculateProfileSpeed(float maximumSpeed, float minimumCornerSpeed, float curvature, float curvatureSpeedScale, float speedFactor)
        {
            var unscaled = Mathf.Max(0f, maximumSpeed) / (1f + Mathf.Max(0f, curvature) * Mathf.Max(0f, curvatureSpeedScale));
            var profileSpeed = Mathf.Clamp(unscaled, Mathf.Max(0f, minimumCornerSpeed), Mathf.Max(0f, maximumSpeed));
            return profileSpeed * Mathf.Clamp(speedFactor, 0f, 1f);
        }

        public static IdealSpeedCommand CalculateSpeedCommand(float currentSpeed, float targetSpeed, float throttleGain, float brakeGain)
        {
            var safeTargetSpeed = Mathf.Max(0.1f, targetSpeed);
            var speedError = targetSpeed - Mathf.Max(0f, currentSpeed);
            if (speedError >= 0f)
            {
                return new IdealSpeedCommand(
                    Mathf.Clamp01(speedError / safeTargetSpeed * Mathf.Max(0f, throttleGain)),
                    0f);
            }

            return new IdealSpeedCommand(
                0f,
                Mathf.Clamp01(-speedError / Mathf.Max(1f, safeTargetSpeed * 0.35f) * Mathf.Max(0f, brakeGain)));
        }
    }

}
