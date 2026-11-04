using System;
using ApexRivals.Career.Runtime;
using ApexRivals.RaceSetup.Configuration;
using UnityEngine;

namespace ApexRivals.Career.Configuration
{
    /// <summary>
    /// Authored career: starter car, car prices and prerequisites, circuit unlocks, the ordered milestones
    /// (objectives, completion and one-time rewards) and the final with its fixed opponent line-up.
    /// </summary>
    [CreateAssetMenu(fileName = "CareerDefinition", menuName = "Apex Rivals/Career/Career Definition")]
    public sealed class CareerDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Granted for free on every profile. Must be a raceable catalog vehicle.")]
        private string starterVehicleId = "vanguard";

        [SerializeField] private VehicleOffer[] vehicleOffers = Array.Empty<VehicleOffer>();

        [SerializeField, Tooltip("Circuits not listed here are open from the start.")]
        private TrackUnlock[] trackUnlocks = Array.Empty<TrackUnlock>();

        [SerializeField, Tooltip("Objective order. Exactly one Win Finale milestone, last.")]
        private Milestone[] milestones = Array.Empty<Milestone>();

        [SerializeField] private Finale finale;

        public RaceSetupDefinition FinaleRaceSetup => finale.raceSetup;

        public CareerConfiguration CreateConfiguration()
        {
            var offers = new CareerVehicleOffer[vehicleOffers.Length];
            for (var index = 0; index < offers.Length; index++)
            {
                offers[index] = new CareerVehicleOffer(vehicleOffers[index].vehicleId, vehicleOffers[index].price, vehicleOffers[index].requiredMilestoneId);
            }

            var unlocks = new CareerTrackUnlock[trackUnlocks.Length];
            for (var index = 0; index < unlocks.Length; index++)
            {
                unlocks[index] = new CareerTrackUnlock(trackUnlocks[index].trackId, trackUnlocks[index].requiredMilestoneId);
            }

            var runtimeMilestones = new CareerMilestone[milestones.Length];
            for (var index = 0; index < runtimeMilestones.Length; index++)
            {
                var milestone = milestones[index];
                runtimeMilestones[index] = new CareerMilestone(
                    milestone.milestoneId,
                    milestone.title,
                    milestone.hint,
                    milestone.kind,
                    milestone.targetId,
                    milestone.targetCount,
                    milestone.currencyReward);
            }

            return new CareerConfiguration(
                starterVehicleId,
                offers,
                unlocks,
                runtimeMilestones,
                new CareerFinale(finale.trackId, finale.title, finale.goal, finale.requiredMilestoneIds ?? Array.Empty<string>()));
        }

        [Serializable]
        private struct VehicleOffer
        {
            public string vehicleId;
            [Min(0)] public int price;
            [Tooltip("Milestone that must be achieved before the car can be bought. Empty: none.")] public string requiredMilestoneId;
        }

        [Serializable]
        private struct TrackUnlock
        {
            public string trackId;
            public string requiredMilestoneId;
        }

        [Serializable]
        private struct Milestone
        {
            [Tooltip("Stable ID written to saves. Never rename a shipped ID.")] public string milestoneId;
            public string title;
            [Tooltip("Next step shown to the player while this is the active objective.")] public string hint;
            public CareerMilestoneKind kind;
            [Tooltip("Track ID for track milestones, upgrade name (Engine/Handling) for upgrade milestones.")] public string targetId;
            [Min(0)] public int targetCount;
            [Min(0)] public int currencyReward;
        }

        [Serializable]
        private struct Finale
        {
            public string trackId;
            public string title;
            public string goal;
            public string[] requiredMilestoneIds;
            [Tooltip("Fixed opponents used instead of the circuit's own race setup.")] public RaceSetupDefinition raceSetup;
        }
    }
}
