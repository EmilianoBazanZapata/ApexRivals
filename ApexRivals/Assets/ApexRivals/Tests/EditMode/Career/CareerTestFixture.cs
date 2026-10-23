using System.Collections.Generic;
using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Tracks.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.VehicleSelection.Runtime;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Career
{
    /// <summary>
    /// In-memory career that mirrors the shipped arc: Vanguard is the starter, Striker is for sale, Apex
    /// Circuit is open, Dunes and Crossover unlock by wins, and the final runs on Crossover.
    /// </summary>
    internal sealed class CareerTestFixture
    {
        public const string Player = "Player";

        public CareerTestFixture(FixtureObjects objects, PlayerProgressionState progression = null, CareerState state = null)
        {
            Progression = progression ?? new PlayerProgressionState();
            Selected = new SelectedVehicleState("vanguard", true);
            State = state ?? new CareerState();
            Save = new FakeSave();
            Vehicles = new VehicleCatalog(new[]
            {
                CreateVehicle(objects, "starter", false),
                CreateVehicle(objects, "vanguard", true),
                CreateVehicle(objects, "striker", true)
            }, "vanguard");
            Tracks = new TrackCatalog(new[]
            {
                new TrackDefinitionData("circuit", "Apex Circuit", string.Empty, 2000f, 3, string.Empty, string.Empty, "Race"),
                new TrackDefinitionData("dunes", "Dunes Circuit", string.Empty, 2000f, 3, string.Empty, string.Empty, "RaceDunes"),
                new TrackDefinitionData("crossover", "Crossover Circuit", string.Empty, 2000f, 3, string.Empty, string.Empty, "RaceSuzuka")
            }, "circuit");
            Configuration = CreateConfiguration();
            Service = new CareerService(Configuration, State, Progression, Selected, Vehicles, Tracks, Save);
            Service.Initialize();
        }

        public PlayerProgressionState Progression { get; }
        public SelectedVehicleState Selected { get; }
        public CareerState State { get; }
        public FakeSave Save { get; }
        public VehicleCatalog Vehicles { get; }
        public TrackCatalog Tracks { get; }
        public CareerConfiguration Configuration { get; }
        public CareerService Service { get; }

        public static readonly IReadOnlyDictionary<UpgradeType, int> MaximumUpgradeLevels = new Dictionary<UpgradeType, int>
        {
            [UpgradeType.Engine] = 3,
            [UpgradeType.Handling] = 3
        };

        public static readonly string[] FinaleRequirements =
        {
            "apex-finish", "apex-win", "engine-1", "dunes-win", "all-circuits", "own-two-cars", "crossover-win", "handling-2"
        };

        public static CareerConfiguration CreateConfiguration(
            IReadOnlyList<CareerMilestone> milestones = null,
            IReadOnlyList<CareerTrackUnlock> unlocks = null,
            IReadOnlyList<CareerVehicleOffer> offers = null,
            CareerFinale finale = null,
            string starter = "vanguard")
        {
            return new CareerConfiguration(
                starter,
                offers ?? new[] { new CareerVehicleOffer("striker", 1800, "apex-win") },
                unlocks ?? new[]
                {
                    new CareerTrackUnlock("dunes", "apex-win"),
                    new CareerTrackUnlock("crossover", "dunes-win")
                },
                milestones ?? DefaultMilestones(),
                finale ?? new CareerFinale("crossover", "Apex Rivals Final", "Finish 1st", FinaleRequirements));
        }

        public static CareerMilestone[] DefaultMilestones()
        {
            return new[]
            {
                new CareerMilestone("apex-finish", "Finish a race at Apex Circuit", "hint", CareerMilestoneKind.CompleteTrack, "circuit", 0, 200),
                new CareerMilestone("apex-win", "Win at Apex Circuit", "hint", CareerMilestoneKind.WinTrack, "circuit", 0, 300),
                new CareerMilestone("engine-1", "Buy an Engine upgrade", "hint", CareerMilestoneKind.ReachUpgradeLevel, "Engine", 1, 150),
                new CareerMilestone("dunes-win", "Win at Dunes Circuit", "hint", CareerMilestoneKind.WinTrack, "dunes", 0, 400),
                new CareerMilestone("all-circuits", "Unlock all 3 circuits", "hint", CareerMilestoneKind.UnlockTracks, string.Empty, 3, 100),
                new CareerMilestone("own-two-cars", "Own 2 cars", "hint", CareerMilestoneKind.OwnVehicles, string.Empty, 2, 300),
                new CareerMilestone("crossover-win", "Win at Crossover Circuit", "hint", CareerMilestoneKind.WinTrack, "crossover", 0, 400),
                new CareerMilestone("handling-2", "Reach Handling level 2", "hint", CareerMilestoneKind.ReachUpgradeLevel, "Handling", 2, 200),
                new CareerMilestone("final-win", "Win the Apex Rivals Final", "hint", CareerMilestoneKind.WinFinale, string.Empty, 0, 2000)
            };
        }

        /// <summary>Track select → race start check → scene install → player finish, like the game does.</summary>
        public CareerRaceOutcome Race(string trackId, int position, bool finale = false)
        {
            var attempt = Start(trackId, finale);
            return Service.RecordRaceFinish(attempt, new RaceResult(Player, position, 60f), Player);
        }

        public CareerRaceAttempt Start(string trackId, bool finale = false)
        {
            var request = Service.PrepareRace(trackId, finale);
            var scene = request.Succeeded ? request.SceneName : Tracks.Tracks[Tracks.IndexOf(trackId)].SceneName;
            var check = Service.CheckRaceStart(scene);
            return check.Allowed ? Service.BeginAttempt(scene, Selected.SelectedVehicleId) : null;
        }

        /// <summary>Plays every milestone needed to qualify for the final (money is added as needed).</summary>
        public void QualifyForFinale()
        {
            Race("circuit", 1);
            Progression.AddCurrency(5000);
            Progression.SetUpgradeLevel(UpgradeType.Engine, 1);
            Progression.SetUpgradeLevel(UpgradeType.Handling, 2);
            Service.EvaluateMilestones();
            Race("dunes", 1);
            Service.PurchaseVehicle("striker");
            Race("crossover", 1);
        }

        private static VehicleDefinitionData CreateVehicle(FixtureObjects objects, string id, bool raceable)
        {
            var configuration = objects.Own(ScriptableObject.CreateInstance<WheelVehicleConfiguration>());
            var prefab = raceable ? objects.Own(new GameObject(id + "Prefab")) : null;
            return new VehicleDefinitionData(id, id, string.Empty, configuration, prefab, null, true);
        }

        internal sealed class FakeSave : ISaveCheckpoint
        {
            public int SaveCount { get; private set; }
            public bool ShouldFail { get; set; }

            public SaveCheckpointResult Save()
            {
                SaveCount++;
                return ShouldFail ? SaveCheckpointResult.Failure("Disk full.") : SaveCheckpointResult.Success();
            }
        }
    }
}
