using UnityEngine;

namespace ApexRivals.DrivingAssistance.Runtime
{
    public enum DrivingLineZone { Green, Yellow, Red }

    public static class DrivingLineRules
    {
        // Speeds are metres per second. The yellow band leaves a margin before braking is overdue.
        public static DrivingLineZone Evaluate(float speed, float recommendedSpeed)
        {
            if (!float.IsFinite(speed) || !float.IsFinite(recommendedSpeed) || recommendedSpeed <= 0) return DrivingLineZone.Red;
            var ratio = Mathf.Max(0, speed) / recommendedSpeed;
            return ratio < 0.85f ? DrivingLineZone.Green : ratio <= 1.05f ? DrivingLineZone.Yellow : DrivingLineZone.Red;
        }

        public static Color32 Color(DrivingLineZone zone, byte alpha) => zone switch
        {
            DrivingLineZone.Green => new Color32(45, 245, 90, alpha),
            DrivingLineZone.Yellow => new Color32(255, 205, 30, alpha),
            _ => new Color32(255, 45, 30, alpha)
        };
    }
}
