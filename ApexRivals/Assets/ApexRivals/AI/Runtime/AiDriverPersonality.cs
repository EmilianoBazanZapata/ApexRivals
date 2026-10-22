using ApexRivals.AI.Configuration;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// The human side of a driver: how far it wanders off the ideal line and the occasional mistake
    /// (braking too late or running wide). Seeded per racer so a rival behaves the same every race.
    /// </summary>
    public sealed class AiDriverPersonality
    {
        private const float WanderFrequency = 0.25f;
        private const float LateBrakingCornerFactor = 1.08f;
        private const float LateBrakingConfidenceFactor = 1.35f;
        private const float RunWideOffset = 2.5f;

        private System.Random _random = new System.Random(1);
        private float _wanderSeed;
        private float _mistakeRemaining;

        /// <summary>The racer id this personality was seeded from.</summary>
        public string SeedId { get; private set; } = string.Empty;

        public bool IsLateBraking { get; private set; }
        public float MistakeOffset { get; private set; }
        public bool IsMistakeActive => _mistakeRemaining > 0f;

        /// <summary>Corner-speed multiplier while a late-braking mistake is active.</summary>
        public float CornerSpeedMultiplier => IsLateBraking ? LateBrakingCornerFactor : 1f;

        /// <summary>Braking-confidence multiplier while a late-braking mistake is active.</summary>
        public float BrakingConfidenceMultiplier => IsLateBraking ? LateBrakingConfidenceFactor : 1f;

        public void Seed(string racerId)
        {
            SeedId = racerId ?? string.Empty;
            var seed = 17;
            if (!string.IsNullOrEmpty(racerId))
            {
                for (var index = 0; index < racerId.Length; index++)
                {
                    seed = seed * 31 + racerId[index];
                }
            }

            _random = new System.Random(seed);
            _wanderSeed = (float)_random.NextDouble() * 100f;
            ClearMistake();
        }

        public void ClearMistake()
        {
            _mistakeRemaining = 0f;
            IsLateBraking = false;
            MistakeOffset = 0f;
        }

        /// <summary>Ends an active mistake when it times out, or rolls for a new one.</summary>
        public void Update(float deltaTime, AiDriverConfiguration configuration, float currentSteering)
        {
            if (_mistakeRemaining > 0f)
            {
                _mistakeRemaining -= deltaTime;
                if (_mistakeRemaining <= 0f)
                {
                    ClearMistake();
                }

                return;
            }

            var interval = configuration.MistakeInterval;
            if (interval <= 0f || _random.NextDouble() >= deltaTime / interval)
            {
                return;
            }

            _mistakeRemaining = Mathf.Max(0.1f, configuration.MistakeDuration);
            if (_random.NextDouble() < 0.5)
            {
                IsLateBraking = true;
            }
            else
            {
                // Running wide drifts to the outside of the current turn.
                var outside = Mathf.Abs(currentSteering) > 0.05f ? -Mathf.Sign(currentSteering) : (_random.NextDouble() < 0.5 ? -1f : 1f);
                MistakeOffset = outside * Mathf.Min(RunWideOffset, configuration.MaximumLateralOffset);
            }
        }

        /// <summary>Smooth, seeded drift around the intended line, in metres.</summary>
        public float CalculateWander(float time, AiDriverConfiguration configuration)
        {
            return configuration.LineWanderAmplitude * (Mathf.PerlinNoise(time * WanderFrequency, _wanderSeed) * 2f - 1f);
        }
    }
}
