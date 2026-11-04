using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.Career
{
    /// <summary>Career persistence through the real profile save service, on in-memory storage only.</summary>
    public sealed class CareerSaveTests
    {
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

        [Test]
        public void NewProfileIsAValidPlayableCareer()
        {
            var storage = new MemoryStorage();
            var progression = new PlayerProgressionState();
            var state = new CareerState();
            var load = CreateSaveService(progression, new SelectedVehicleState(), state, storage).LoadOrCreateDefault();
            var career = new CareerTestFixture(_objects, progression, state);

            Assert.That(load.Status, Is.EqualTo(PlayerProfileLoadStatus.CreatedDefault));
            Assert.That(load.SaveData.schemaVersion, Is.EqualTo(4));
            Assert.That(career.Service.IsVehicleOwned("vanguard"), Is.True);
            Assert.That(career.Service.PrepareRace("circuit", false).Succeeded, Is.True);
        }

        [Test]
        public void CareerFactsSurviveARestart()
        {
            var storage = new MemoryStorage();
            var first = new CareerTestFixture(_objects);
            first.QualifyForFinale();
            first.Race("crossover", 1, finale: true);
            Assert.That(CreateSaveService(first.Progression, first.Selected, first.State, storage).Save().Succeeded, Is.True);

            var progression = new PlayerProgressionState();
            var selected = new SelectedVehicleState();
            var state = new CareerState();
            var load = CreateSaveService(progression, selected, state, storage).LoadOrCreateDefault();
            var restarted = new CareerTestFixture(_objects, progression, state);

            Assert.That(load.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded));
            Assert.That(state.OwnedVehicleIds, Is.EquivalentTo(new[] { "vanguard", "striker" }));
            Assert.That(state.WonTrackIds, Is.EquivalentTo(new[] { "circuit", "dunes", "crossover" }));
            Assert.That(state.FinaleWon, Is.True);
            Assert.That(progression.Currency, Is.EqualTo(first.Progression.Currency));
            Assert.That(restarted.Service.GetSummary().CompletionPercent, Is.EqualTo(100));
            Assert.That(restarted.Service.EvaluateMilestones(), Is.Empty, "Reloading never pays a reward again.");
        }

        [Test]
        public void SchemaThreeProfileMigratesKeepingCoreProgressAndTheSelectedCar()
        {
            var storage = new MemoryStorage
            {
                SavePayload = "{ \"schemaVersion\": 3, \"currency\": 640, \"engineUpgradeLevel\": 2, \"handlingUpgradeLevel\": 1, \"completedRaceCount\": 7, \"selectedVehicleId\": \"striker\", \"vehicleSelectionCommitted\": true }"
            };
            var progression = new PlayerProgressionState();
            var selected = new SelectedVehicleState();
            var state = new CareerState();

            var load = CreateSaveService(progression, selected, state, storage).LoadOrCreateDefault();
            var career = new CareerTestFixture(_objects, progression, state);

            Assert.That(load.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded), load.Message);
            Assert.That(progression.CompletedRaceCount, Is.EqualTo(7));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(2));
            Assert.That(progression.GetUpgradeLevel(UpgradeType.Handling), Is.EqualTo(1));
            Assert.That(selected.SelectedVehicleId, Is.EqualTo("striker"));
            Assert.That(career.Service.IsVehicleOwned("striker"), Is.True, "The previously selected car stays raceable.");
            Assert.That(career.Service.IsVehicleOwned("vanguard"), Is.True, "The starter is granted.");
            Assert.That(state.CompletedTrackIds, Is.Empty, "No track result is invented from the old race count.");
            Assert.That(state.WonTrackIds, Is.Empty);
            Assert.That(state.HasAchieved("apex-win"), Is.False);
            Assert.That(state.HasAchieved("engine-1"), Is.True, "Facts that really exist (upgrades, cars) count.");
            Assert.That(progression.Currency, Is.EqualTo(640 + 150 + 300), "Old currency kept; milestones met by real facts pay once.");
            Assert.That(storage.SavePayload, Does.Contain("\"schemaVersion\": 3"), "Loading alone never rewrites the file.");
        }

        [Test]
        public void SchemaThreeWithoutCommittedCarOwnsOnlyTheStarter()
        {
            var storage = new MemoryStorage
            {
                SavePayload = "{ \"schemaVersion\": 3, \"currency\": 10, \"selectedVehicleId\": \"striker\", \"vehicleSelectionCommitted\": false }"
            };
            var state = new CareerState();
            var progression = new PlayerProgressionState();

            CreateSaveService(progression, new SelectedVehicleState(), state, storage).LoadOrCreateDefault();
            var career = new CareerTestFixture(_objects, progression, state);

            Assert.That(career.Service.IsVehicleOwned("striker"), Is.False);
            Assert.That(state.OwnedVehicleIds, Is.EqualTo(new[] { "vanguard" }));
        }

        [Test]
        public void NewerSchemaIsNeverReplacedByABackup()
        {
            var newer = "{ \"schemaVersion\": 9, \"currency\": 99999 }";
            var backup = Serialize(PlayerProfileSaveData.CreateDefault());
            var storage = new MemoryStorage { SavePayload = newer, BackupPayload = backup };
            var service = CreateSaveService(new PlayerProgressionState(), new SelectedVehicleState(), new CareerState(), storage);

            var load = service.LoadOrCreateDefault();

            Assert.That(load.Status, Is.EqualTo(PlayerProfileLoadStatus.Failed));
            Assert.That(storage.SavePayload, Is.EqualTo(newer));
            Assert.That(storage.BackupPayload, Is.EqualTo(backup));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void DuplicateBlankAndUnknownIdsAreNormalizedWithoutLosingProgress()
        {
            var data = PlayerProfileSaveData.CreateDefault();
            data.career = new CareerSaveData
            {
                ownedVehicleIds = new[] { "vanguard", " vanguard", "", null, "future-car" },
                completedTrackIds = new[] { "circuit", "circuit" },
                wonTrackIds = new[] { "circuit" },
                achievedMilestoneIds = new[] { "apex-finish", "apex-finish", "future-milestone" },
                finaleWon = false
            };
            var storage = new MemoryStorage { SavePayload = Serialize(data) };
            var state = new CareerState();

            var load = CreateSaveService(new PlayerProgressionState(), new SelectedVehicleState(), state, storage).LoadOrCreateDefault();

            Assert.That(load.Status, Is.EqualTo(PlayerProfileLoadStatus.Loaded));
            Assert.That(state.OwnedVehicleIds, Is.EqualTo(new[] { "vanguard", "future-car" }));
            Assert.That(state.CompletedTrackIds, Is.EqualTo(new[] { "circuit" }));
            Assert.That(state.AchievedMilestoneIds, Is.EqualTo(new[] { "apex-finish", "future-milestone" }));
        }

        [Test]
        public void SaveFailureIsReportedAndNothingIsWritten()
        {
            var storage = new MemoryStorage { FailWrites = true };
            var career = new CareerTestFixture(_objects);
            career.Race("circuit", 1);

            var result = CreateSaveService(career.Progression, career.Selected, career.State, storage).Save();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(storage.SavePayload, Is.Null);
        }

        private static PlayerProfileSaveService CreateSaveService(PlayerProgressionState progression, SelectedVehicleState selected, CareerState state, MemoryStorage storage)
        {
            return new PlayerProfileSaveService(progression, selected, storage, new[] { "vanguard", "striker" }, state);
        }

        private static string Serialize(PlayerProfileSaveData data)
        {
            var result = new PlayerProfileJsonSerializer().Serialize(data);
            Assert.That(result.Succeeded, Is.True);
            return result.Json;
        }

        private sealed class MemoryStorage : IPlayerProfileSaveStorage
        {
            public string SavePayload { get; set; }
            public string BackupPayload { get; set; }
            public bool FailWrites { get; set; }
            public int WriteCount { get; private set; }

            public bool SaveExists() => SavePayload != null;
            public bool BackupExists() => BackupPayload != null;

            public SaveStorageReadResult ReadSave() => SaveExists()
                ? new SaveStorageReadResult(true, SavePayload, string.Empty)
                : new SaveStorageReadResult(false, string.Empty, "Missing.");

            public SaveStorageReadResult ReadBackup() => BackupExists()
                ? new SaveStorageReadResult(true, BackupPayload, string.Empty)
                : new SaveStorageReadResult(false, string.Empty, "Missing.");

            public SaveStorageWriteResult WriteSave(string payload, bool preserveBackup = false)
            {
                if (FailWrites)
                {
                    return new SaveStorageWriteResult(false, "Write failed.");
                }

                WriteCount++;
                if (SavePayload != null && !preserveBackup)
                {
                    BackupPayload = SavePayload;
                }

                SavePayload = payload;
                return new SaveStorageWriteResult(true, string.Empty);
            }

            public SaveStorageWriteResult DeleteSave()
            {
                SavePayload = null;
                BackupPayload = null;
                return new SaveStorageWriteResult(true, string.Empty);
            }
        }
    }
}
