using System.Collections.Generic;
using ApexRivals.Career.Configuration;
using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Configuration;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Configuration;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.Tracks.Configuration;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VehicleSelection.Configuration;
using NUnit.Framework;
using UnityEditor;

namespace ApexRivals.Tests.EditMode.Career
{
    /// <summary>Checks the shipped career asset against the real catalogs, rewards and upgrade prices.</summary>
    public sealed class CareerContentTests
    {
        private const string Root = "Assets/ApexRivals/Content/";

        [Test]
        public void ShippedCareerIsValidAgainstTheRealCatalogs()
        {
            var assets = Load();

            var validation = assets.Configuration.Validate(assets.Vehicles, assets.Tracks, assets.MaximumLevels);

            Assert.That(validation.Succeeded, Is.True, validation.Message);
            Assert.That(assets.Definition.FinaleRaceSetup, Is.Not.Null);
            Assert.That(assets.Definition.FinaleRaceSetup.AiOpponents.Count, Is.EqualTo(3));
            Assert.That(assets.Configuration.StarterVehicleId, Is.EqualTo(assets.Vehicles.DefaultVehicleId), "A fresh profile starts in the granted car.");
            Assert.That(assets.Tracks.DefaultTrack.TrackId, Is.EqualTo("circuit"));
        }

        /// <summary>
        /// A fresh player who loses the first race on each goal and wins the second, buys what the objective
        /// asks for as soon as it is affordable, and otherwise races. Documents the expected session length.
        /// </summary>
        [Test]
        public void FreshPlayerFinishesTheCareerInAShortSessionWithoutDeadlocks()
        {
            var assets = Load();
            var progression = new PlayerProgressionState();
            var selected = new SelectedVehicleState(assets.Vehicles.DefaultVehicleId, true);
            var career = new CareerService(assets.Configuration, new CareerState(), progression, selected, assets.Vehicles, assets.Tracks);
            career.Initialize();
            var garage = new GarageService(progression, assets.Engine, assets.Handling, default(VehiclePerformanceStats));
            garage.UpgradePurchased += (_, _) => career.EvaluateMilestones();
            var rewards = new RaceRewardService(progression, assets.Rewards);
            var races = 0;
            var lastTrack = "circuit";

            while (!career.State.FinaleWon && races < 40)
            {
                var objective = career.GetSummary().Objective;
                var milestone = assets.Configuration.FindMilestone(objective.MilestoneId);
                Assert.That(milestone, Is.Not.Null, "There is always an active objective until the final is won.");

                if (milestone.Kind == CareerMilestoneKind.ReachUpgradeLevel
                    && CareerConfiguration.TryParseUpgrade(milestone.TargetId, out var upgrade)
                    && garage.Purchase(upgrade).Succeeded)
                {
                    continue;
                }

                if (milestone.Kind == CareerMilestoneKind.OwnVehicles && TryBuyAnyCar(career, assets))
                {
                    continue;
                }

                var isFinale = milestone.Kind == CareerMilestoneKind.WinFinale;
                var track = isFinale ? assets.Configuration.Finale.TrackId
                    : (milestone.Kind == CareerMilestoneKind.CompleteTrack || milestone.Kind == CareerMilestoneKind.WinTrack) && career.IsTrackUnlocked(milestone.TargetId)
                        ? milestone.TargetId
                        : lastTrack;
                lastTrack = track;
                races++;
                var position = races % 2 == 1 ? 2 : 1;
                var request = career.PrepareRace(track, isFinale);
                Assert.That(request.Succeeded, Is.True, request.Message);
                Assert.That(career.CheckRaceStart(request.SceneName).Allowed, Is.True);
                var attempt = career.BeginAttempt(request.SceneName, selected.SelectedVehicleId);
                var result = new RaceResult("Player", position, 120f);
                Assert.That(rewards.TryAwardReward(result, out _), Is.True);
                career.RecordRaceFinish(attempt, result, "Player");
            }

            UnityEngine.Debug.Log($"Career simulation: finished in {races} races with ${progression.Currency} left.");
            Assert.That(career.State.FinaleWon, Is.True);
            Assert.That(career.GetSummary().CompletionPercent, Is.EqualTo(100));
            Assert.That(races, Is.LessThanOrEqualTo(14), "The career should not need grinding.");
        }

        private static bool TryBuyAnyCar(CareerService career, Assets assets)
        {
            foreach (var offer in assets.Configuration.VehicleOffers)
            {
                if (career.PurchaseVehicle(offer.VehicleId).Succeeded)
                {
                    return true;
                }
            }

            return false;
        }

        private static Assets Load()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CareerDefinition>(Root + "Configuration/Career/CareerDefinition.asset");
            var vehicles = AssetDatabase.LoadAssetAtPath<VehicleCatalogDefinition>(Root + "Configuration/Vehicles/VehicleCatalogDefinition.asset");
            var tracks = AssetDatabase.LoadAssetAtPath<TrackCatalogDefinition>(Root + "Tracks/TrackCatalogDefinition.asset");
            var engine = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(Root + "Configuration/Garage/EngineUpgradeDefinition.asset");
            var handling = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(Root + "Configuration/Garage/HandlingUpgradeDefinition.asset");
            var rewards = AssetDatabase.LoadAssetAtPath<RaceRewardDefinition>(Root + "Configuration/Progression/RaceRewardDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(vehicles, Is.Not.Null);
            Assert.That(tracks, Is.Not.Null);
            Assert.That(engine, Is.Not.Null);
            Assert.That(handling, Is.Not.Null);
            Assert.That(rewards, Is.Not.Null);
            return new Assets(definition, vehicles, tracks, engine, handling, rewards);
        }

        private sealed class Assets
        {
            public Assets(CareerDefinition definition, VehicleCatalogDefinition vehicles, TrackCatalogDefinition tracks, UpgradeDefinition engine, UpgradeDefinition handling, RaceRewardDefinition rewards)
            {
                Definition = definition;
                Configuration = definition.CreateConfiguration();
                Vehicles = vehicles.CreateCatalog();
                Tracks = tracks.CreateCatalog();
                Engine = engine.CreateData();
                Handling = handling.CreateData();
                Rewards = rewards.CreateRewardTable();
                MaximumLevels = new Dictionary<UpgradeType, int>
                {
                    [UpgradeType.Engine] = Engine.MaximumLevel,
                    [UpgradeType.Handling] = Handling.MaximumLevel
                };
            }

            public CareerDefinition Definition { get; }
            public CareerConfiguration Configuration { get; }
            public ApexRivals.VehicleSelection.Runtime.VehicleCatalog Vehicles { get; }
            public ApexRivals.Tracks.Runtime.TrackCatalog Tracks { get; }
            public UpgradeDefinitionData Engine { get; }
            public UpgradeDefinitionData Handling { get; }
            public RaceRewardTable Rewards { get; }
            public IReadOnlyDictionary<UpgradeType, int> MaximumLevels { get; }
        }
    }
}
