using System;
using System.Collections.Generic;
using System.Globalization;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Configuration;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Tracks.Runtime;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.Career.Runtime
{
    /// <summary>
    /// Owns every career rule: car ownership and purchases, circuit unlocks, milestones and their one-time
    /// rewards, the final and completion. Currency stays in <see cref="PlayerProgressionState"/>; facts live in
    /// <see cref="CareerState"/> and are persisted with the player profile. UI only reads and requests.
    /// </summary>
    public sealed class CareerService : IVehicleOwnership, IRaceStartGuard
    {
        private readonly CareerConfiguration _configuration;
        private readonly CareerState _state;
        private readonly PlayerProgressionState _progression;
        private readonly SelectedVehicleState _selectedVehicle;
        private readonly VehicleCatalog _vehicles;
        private readonly TrackCatalog _tracks;
        private readonly ISaveCheckpoint _saveCheckpoint;
        private readonly HashSet<int> _processedAttempts = new HashSet<int>();
        private readonly List<CareerMilestoneAward> _announcements = new List<CareerMilestoneAward>();
        private PendingRace _prepared;
        private PendingRace _armed;
        private int _lastAttemptId;
        private bool _purchaseInProgress;

        public CareerService(
            CareerConfiguration configuration,
            CareerState state,
            PlayerProgressionState progression,
            SelectedVehicleState selectedVehicle,
            VehicleCatalog vehicles,
            TrackCatalog tracks,
            ISaveCheckpoint saveCheckpoint = null,
            RaceSetupDefinition finaleRaceSetup = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _selectedVehicle = selectedVehicle ?? throw new ArgumentNullException(nameof(selectedVehicle));
            _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            _saveCheckpoint = saveCheckpoint;
            FinaleRaceSetup = finaleRaceSetup;
        }

        /// <summary>Raised after any career fact, unlock or objective may have changed.</summary>
        public event Action Changed;

        public CareerConfiguration Configuration => _configuration;
        public CareerState State => _state;
        public TrackCatalog Tracks => _tracks;

        /// <summary>Fixed opponent line-up used instead of the circuit's own when the final is raced.</summary>
        public RaceSetupDefinition FinaleRaceSetup { get; }

        /// <summary>
        /// Grants the starter car and records milestones already satisfied by loaded facts (for example an
        /// upgrade bought before the career existed). Call after the profile is loaded.
        /// </summary>
        public void Initialize()
        {
            var granted = _state.GrantVehicle(_configuration.StarterVehicleId);
            var awards = EvaluateMilestonesInternal();
            _announcements.AddRange(awards);
            if (granted || awards.Count > 0)
            {
                Changed?.Invoke();
            }
        }

        public bool IsVehicleOwned(string vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return false;
            }

            var id = vehicleId.Trim();
            return id == _configuration.StarterVehicleId || _state.OwnsVehicle(id);
        }

        public CareerVehicleStatus GetVehicleStatus(string vehicleId)
        {
            var id = vehicleId?.Trim() ?? string.Empty;
            var offer = _configuration.FindOffer(id);
            var requiredId = offer?.RequiredMilestoneId ?? string.Empty;
            var price = offer?.Price ?? 0;
            return new CareerVehicleStatus(
                id,
                id == _configuration.StarterVehicleId,
                IsVehicleOwned(id),
                offer != null,
                price,
                string.IsNullOrEmpty(requiredId) || _state.HasAchieved(requiredId),
                _configuration.FindMilestone(requiredId)?.Title,
                _progression.CanAfford(price));
        }

        /// <summary>
        /// Buys a car: validates the ID, ownership, prerequisite and funds, then deducts the price and grants the
        /// car once and saves. Any failure, a failed save included, leaves currency and ownership unchanged.
        /// </summary>
        public CareerVehiclePurchaseResult PurchaseVehicle(string vehicleId)
        {
            var id = vehicleId?.Trim() ?? string.Empty;
            if (_purchaseInProgress)
            {
                return PurchaseResult(CareerVehiclePurchaseStatus.Busy, id, 0, null, "A purchase is already in progress.");
            }

            var lookup = _vehicles.FindById(id);
            if (!lookup.Succeeded)
            {
                return PurchaseResult(CareerVehiclePurchaseStatus.UnknownVehicle, id, 0, null, lookup.Message);
            }

            if (IsVehicleOwned(id))
            {
                return PurchaseResult(CareerVehiclePurchaseStatus.AlreadyOwned, id, 0, null, "The car is already owned.");
            }

            var offer = _configuration.FindOffer(id);
            if (offer == null || !VehicleCatalog.IsRaceSelectable(lookup.Vehicle))
            {
                return PurchaseResult(CareerVehiclePurchaseStatus.NotForSale, id, 0, null, "The car is not for sale.");
            }

            if (!string.IsNullOrEmpty(offer.RequiredMilestoneId) && !_state.HasAchieved(offer.RequiredMilestoneId))
            {
                var required = _configuration.FindMilestone(offer.RequiredMilestoneId);
                return PurchaseResult(CareerVehiclePurchaseStatus.PrerequisiteNotMet, id, offer.Price, null, "Requires: " + (required?.Title ?? offer.RequiredMilestoneId));
            }

            if (!_progression.CanAfford(offer.Price))
            {
                return PurchaseResult(CareerVehiclePurchaseStatus.InsufficientFunds, id, offer.Price, null, "Not enough currency.");
            }

            _purchaseInProgress = true;
            try
            {
                var currencyBefore = _progression.Currency;
                var stateBefore = _state.Clone();
                if (!_progression.SpendCurrency(offer.Price).Succeeded)
                {
                    return PurchaseResult(CareerVehiclePurchaseStatus.InsufficientFunds, id, offer.Price, null, "Not enough currency.");
                }

                _state.GrantVehicle(id);
                var awards = EvaluateMilestonesInternal();
                if (_saveCheckpoint != null)
                {
                    var save = _saveCheckpoint.Save();
                    if (!save.Succeeded)
                    {
                        // Roll the whole transaction back so nothing unsaved is presented as bought.
                        _state.CopyFrom(stateBefore);
                        _progression.SetCurrency(currencyBefore);
                        Changed?.Invoke();
                        return PurchaseResult(CareerVehiclePurchaseStatus.SaveFailed, id, offer.Price, null, save.Message);
                    }
                }

                _announcements.AddRange(awards);
                Changed?.Invoke();
                return PurchaseResult(CareerVehiclePurchaseStatus.Purchased, id, offer.Price, awards, string.Empty);
            }
            finally
            {
                _purchaseInProgress = false;
            }
        }

        public bool IsTrackUnlocked(string trackId)
        {
            if (_tracks.IndexOf(trackId) < 0)
            {
                return false;
            }

            var unlock = _configuration.FindTrackUnlock(trackId);
            return unlock == null || _state.HasAchieved(unlock.RequiredMilestoneId);
        }

        public CareerTrackStatus GetTrackStatus(string trackId)
        {
            var known = _tracks.IndexOf(trackId) >= 0;
            var unlock = _configuration.FindTrackUnlock(trackId);
            return new CareerTrackStatus(
                trackId,
                known,
                known && IsTrackUnlocked(trackId),
                unlock != null ? _configuration.FindMilestone(unlock.RequiredMilestoneId)?.Title : string.Empty,
                _state.HasCompletedTrack(trackId),
                _state.HasWonTrack(trackId));
        }

        public CareerFinaleStatus GetFinaleStatus()
        {
            var finale = _configuration.Finale;
            if (finale == null)
            {
                return new CareerFinaleStatus(CareerFinaleState.Locked, string.Empty, string.Empty, string.Empty, 0, 0, string.Empty);
            }

            var met = 0;
            var nextMissing = string.Empty;
            for (var index = 0; index < finale.RequiredMilestoneIds.Count; index++)
            {
                var requiredId = finale.RequiredMilestoneIds[index];
                if (_state.HasAchieved(requiredId))
                {
                    met++;
                }
                else if (string.IsNullOrEmpty(nextMissing))
                {
                    nextMissing = _configuration.FindMilestone(requiredId)?.Title ?? requiredId;
                }
            }

            var total = finale.RequiredMilestoneIds.Count;
            var state = _state.FinaleWon
                ? CareerFinaleState.Won
                : met == total && IsTrackUnlocked(finale.TrackId) ? CareerFinaleState.Available : CareerFinaleState.Locked;
            if (state == CareerFinaleState.Locked && string.IsNullOrEmpty(nextMissing))
            {
                nextMissing = GetTrackStatus(finale.TrackId).RequirementTitle;
            }

            return new CareerFinaleStatus(state, finale.Title, finale.Goal, finale.TrackId, met, total, nextMissing);
        }

        public CareerSummary GetSummary()
        {
            var achieved = CountAchievedMilestones();
            var owned = CountOwnedVehicles();
            return new CareerSummary(
                ComputeCompletionPercent(achieved, _configuration.Milestones.Count, _state.FinaleWon),
                achieved,
                _configuration.Milestones.Count,
                CreateObjective(),
                owned,
                Math.Max(0, owned - (IsVehicleOwned(_configuration.StarterVehicleId) ? 1 : 0)),
                _vehicles.GetRaceSelectableVehicles().Count,
                CountUnlockedTracks(),
                _tracks.Count,
                GetFinaleStatus());
        }

        /// <summary>
        /// Completion from achieved milestones only: floor(achieved * 100 / total), never above 100 and held at
        /// 99 until the final is won. Currency and spending never enter it.
        /// </summary>
        public static int ComputeCompletionPercent(int achievedMilestones, int totalMilestones, bool finaleWon)
        {
            if (totalMilestones <= 0)
            {
                return 0;
            }

            var achieved = Math.Max(0, Math.Min(achievedMilestones, totalMilestones));
            var percent = (int)((long)achieved * 100 / totalMilestones);
            return !finaleWon && percent >= 100 ? 99 : percent;
        }

        /// <summary>Validates and remembers the race chosen on the track select screen.</summary>
        public CareerRaceRequestResult PrepareRace(string trackId, bool isFinale)
        {
            var id = isFinale ? _configuration.Finale?.TrackId ?? string.Empty : trackId?.Trim() ?? string.Empty;
            var index = _tracks.IndexOf(id);
            if (index < 0)
            {
                return RaceRequest(CareerRaceRequestStatus.UnknownTrack, id, isFinale, "Unknown circuit.");
            }

            var validation = ValidateRace(id, isFinale);
            if (validation != CareerRaceRequestStatus.Ready)
            {
                return RaceRequest(validation, id, isFinale, DescribeRejection(validation, id));
            }

            _prepared = new PendingRace(id, _tracks.Tracks[index].SceneName, isFinale);
            return new CareerRaceRequestResult(CareerRaceRequestStatus.Ready, id, _prepared.SceneName, isFinale, string.Empty);
        }

        /// <summary>
        /// Service-level check run by scene flow before a race scene loads. It accepts the race prepared on the
        /// track select screen for that scene, or a normal race on the circuit the scene belongs to, and arms it
        /// so the race scene (and its retries) know which event they are.
        /// </summary>
        public RaceStartCheckResult CheckRaceStart(string raceSceneName)
        {
            var prepared = _prepared;
            _prepared = null;
            var race = prepared != null && prepared.SceneName == raceSceneName ? prepared : CreateNormalRace(raceSceneName);
            if (race == null)
            {
                // Not a career circuit: let it run, it simply cannot count.
                _armed = null;
                return VehicleCheck();
            }

            var validation = ValidateRace(race.TrackId, race.IsFinale);
            if (validation != CareerRaceRequestStatus.Ready)
            {
                return RaceStartCheckResult.Reject(DescribeRejection(validation, race.TrackId));
            }

            _armed = race;
            return RaceStartCheckResult.Allow();
        }

        /// <summary>
        /// Captures the identity of a race attempt as its scene installs. The circuit comes from the scene
        /// actually loaded; only a race armed for that same scene can be the final.
        /// </summary>
        public CareerRaceAttempt BeginAttempt(string loadedSceneName, string vehicleId)
        {
            var attemptId = ++_lastAttemptId;
            var race = _armed != null && _armed.SceneName == loadedSceneName ? _armed : CreateNormalRace(loadedSceneName);
            if (race == null)
            {
                return new CareerRaceAttempt(attemptId, string.Empty, vehicleId, false, false, "The circuit is not part of the career.");
            }

            var counts = IsTrackUnlocked(race.TrackId)
                && IsVehicleOwned(vehicleId)
                && (!race.IsFinale || GetFinaleStatus().Qualified);
            return new CareerRaceAttempt(attemptId, race.TrackId, vehicleId, race.IsFinale, counts, counts ? string.Empty : "The race does not meet the career requirements.");
        }

        /// <summary>
        /// Records the player's finish for an attempt, exactly once: track completion and win facts, the final,
        /// newly achieved milestones and their one-time rewards. Abandoned attempts never reach this.
        /// </summary>
        public CareerRaceOutcome RecordRaceFinish(CareerRaceAttempt attempt, RaceResult result, string playerParticipantId)
        {
            if (attempt == null)
            {
                return Outcome(CareerRaceOutcomeStatus.NotCounted, null, false, false);
            }

            if (_processedAttempts.Contains(attempt.AttemptId))
            {
                return Outcome(CareerRaceOutcomeStatus.AlreadyProcessed, attempt.TrackId, attempt.IsFinale, false);
            }

            if (string.IsNullOrWhiteSpace(playerParticipantId)
                || !string.Equals(result.RacerId, playerParticipantId, StringComparison.Ordinal)
                || result.FinalPosition < 1)
            {
                return Outcome(CareerRaceOutcomeStatus.InvalidResult, attempt.TrackId, attempt.IsFinale, false);
            }

            _processedAttempts.Add(attempt.AttemptId);
            var won = result.FinalPosition == 1;
            if (!attempt.Counts)
            {
                return Outcome(CareerRaceOutcomeStatus.NotCounted, attempt.TrackId, attempt.IsFinale, won);
            }

            var completionBefore = GetSummary().CompletionPercent;
            var finaleWonBefore = _state.FinaleWon;
            var unlockedBefore = SnapshotUnlockedTracks();
            var purchasableBefore = SnapshotPurchasableVehicles();

            _state.MarkTrackCompleted(attempt.TrackId);
            if (won)
            {
                _state.MarkTrackWon(attempt.TrackId);
                if (attempt.IsFinale)
                {
                    _state.MarkFinaleWon();
                }
            }

            var awards = EvaluateMilestonesInternal();
            var summary = GetSummary();
            Changed?.Invoke();
            return new CareerRaceOutcome(
                CareerRaceOutcomeStatus.Recorded,
                attempt.TrackId,
                attempt.IsFinale,
                won,
                awards,
                NewTrackNames(unlockedBefore),
                NewVehicleNames(purchasableBefore),
                completionBefore,
                summary.CompletionPercent,
                !finaleWonBefore && _state.FinaleWon,
                summary);
        }

        /// <summary>Records milestones satisfied outside races (upgrades) and pays their one-time rewards.</summary>
        public IReadOnlyList<CareerMilestoneAward> EvaluateMilestones()
        {
            var awards = EvaluateMilestonesInternal();
            if (awards.Count > 0)
            {
                _announcements.AddRange(awards);
                Changed?.Invoke();
            }

            return awards;
        }

        /// <summary>Returns milestones achieved outside a results screen since the last call, once.</summary>
        public IReadOnlyList<CareerMilestoneAward> ConsumeAnnouncements()
        {
            if (_announcements.Count == 0)
            {
                return Array.Empty<CareerMilestoneAward>();
            }

            var copy = _announcements.ToArray();
            _announcements.Clear();
            return copy;
        }

        public string GetTrackName(string trackId)
        {
            var index = _tracks.IndexOf(trackId);
            return index >= 0 ? _tracks.Tracks[index].DisplayName : trackId ?? string.Empty;
        }

        public string GetVehicleName(string vehicleId)
        {
            var lookup = _vehicles.FindById(vehicleId);
            return lookup.Succeeded ? lookup.Vehicle.DisplayName : vehicleId ?? string.Empty;
        }

        private List<CareerMilestoneAward> EvaluateMilestonesInternal()
        {
            var awards = new List<CareerMilestoneAward>();
            bool changed;
            do
            {
                // Unlocks feed later milestones (e.g. "unlock all circuits"), so repeat until stable.
                changed = false;
                foreach (var milestone in _configuration.Milestones)
                {
                    if (_state.HasAchieved(milestone.MilestoneId) || !IsSatisfied(milestone))
                    {
                        continue;
                    }

                    _state.MarkMilestoneAchieved(milestone.MilestoneId);
                    awards.Add(new CareerMilestoneAward(
                        milestone.MilestoneId,
                        milestone.Title,
                        Credit(milestone.CurrencyReward),
                        milestone.Kind == CareerMilestoneKind.WinFinale));
                    changed = true;
                }
            }
            while (changed);

            return awards;
        }

        private bool IsSatisfied(CareerMilestone milestone)
        {
            switch (milestone.Kind)
            {
                case CareerMilestoneKind.CompleteTrack:
                    return _state.HasCompletedTrack(milestone.TargetId);
                case CareerMilestoneKind.WinTrack:
                    return _state.HasWonTrack(milestone.TargetId);
                case CareerMilestoneKind.ReachUpgradeLevel:
                    return CareerConfiguration.TryParseUpgrade(milestone.TargetId, out var upgrade)
                        && _progression.GetUpgradeLevel(upgrade) >= milestone.TargetCount;
                case CareerMilestoneKind.OwnVehicles:
                    return CountOwnedVehicles() >= milestone.TargetCount;
                case CareerMilestoneKind.UnlockTracks:
                    return CountUnlockedTracks() >= milestone.TargetCount;
                case CareerMilestoneKind.WinFinale:
                    return _state.FinaleWon;
                default:
                    return false;
            }
        }

        private int Credit(int reward)
        {
            // Same policy as race rewards: a full wallet only credits what fits.
            var credited = Math.Min(Math.Max(0, reward), int.MaxValue - _progression.Currency);
            return credited > 0 && _progression.AddCurrency(credited).Succeeded ? credited : 0;
        }

        private CareerObjective CreateObjective()
        {
            foreach (var milestone in _configuration.Milestones)
            {
                if (_state.HasAchieved(milestone.MilestoneId))
                {
                    continue;
                }

                return new CareerObjective(
                    milestone.MilestoneId,
                    milestone.Title,
                    CreateHint(milestone),
                    CreateProgressText(milestone),
                    milestone.CurrencyReward,
                    milestone.Kind == CareerMilestoneKind.WinFinale);
            }

            return default;
        }

        private string CreateHint(CareerMilestone milestone)
        {
            switch (milestone.Kind)
            {
                case CareerMilestoneKind.CompleteTrack:
                case CareerMilestoneKind.WinTrack:
                    if (!IsTrackUnlocked(milestone.TargetId))
                    {
                        return $"{GetTrackName(milestone.TargetId)} is locked. First: {GetTrackStatus(milestone.TargetId).RequirementTitle}.";
                    }

                    break;
                case CareerMilestoneKind.WinFinale:
                    var finale = GetFinaleStatus();
                    if (finale.State == CareerFinaleState.Locked)
                    {
                        return $"The final is locked. First: {finale.NextMissingTitle}.";
                    }

                    break;
            }

            return milestone.Hint;
        }

        private string CreateProgressText(CareerMilestone milestone)
        {
            switch (milestone.Kind)
            {
                case CareerMilestoneKind.OwnVehicles:
                    return string.Format(CultureInfo.InvariantCulture, "{0} / {1} CARS", Math.Min(CountOwnedVehicles(), milestone.TargetCount), milestone.TargetCount);
                case CareerMilestoneKind.UnlockTracks:
                    return string.Format(CultureInfo.InvariantCulture, "{0} / {1} CIRCUITS", Math.Min(CountUnlockedTracks(), milestone.TargetCount), milestone.TargetCount);
                case CareerMilestoneKind.ReachUpgradeLevel:
                    if (!CareerConfiguration.TryParseUpgrade(milestone.TargetId, out var upgrade))
                    {
                        return string.Empty;
                    }

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} LEVEL {1} / {2}",
                        upgrade.ToString().ToUpperInvariant(),
                        Math.Min(_progression.GetUpgradeLevel(upgrade), milestone.TargetCount),
                        milestone.TargetCount);
                case CareerMilestoneKind.WinFinale:
                    var finale = GetFinaleStatus();
                    return string.Format(CultureInfo.InvariantCulture, "{0} / {1} REQUIREMENTS", finale.MetRequirements, finale.TotalRequirements);
                default:
                    return string.Empty;
            }
        }

        private CareerRaceRequestStatus ValidateRace(string trackId, bool isFinale)
        {
            if (!_selectedVehicle.HasCommittedVehicleSelection)
            {
                return CareerRaceRequestStatus.NoVehicle;
            }

            if (!IsVehicleOwned(_selectedVehicle.SelectedVehicleId))
            {
                return CareerRaceRequestStatus.VehicleNotOwned;
            }

            // The final's qualification already includes its circuit being open.
            if (isFinale)
            {
                return _configuration.Finale != null && trackId == _configuration.Finale.TrackId && GetFinaleStatus().Qualified
                    ? CareerRaceRequestStatus.Ready
                    : CareerRaceRequestStatus.FinaleLocked;
            }

            var index = _tracks.IndexOf(trackId);
            if (index >= 0 && _tracks.Tracks[index].FinaleOnly)
            {
                return CareerRaceRequestStatus.FinaleLocked;
            }

            return IsTrackUnlocked(trackId) ? CareerRaceRequestStatus.Ready : CareerRaceRequestStatus.TrackLocked;
        }

        private string DescribeRejection(CareerRaceRequestStatus status, string trackId)
        {
            return status switch
            {
                CareerRaceRequestStatus.NoVehicle => "Select a car first.",
                CareerRaceRequestStatus.VehicleNotOwned => "The selected car is not owned.",
                CareerRaceRequestStatus.TrackLocked => $"{GetTrackName(trackId)} is locked. Requires: {GetTrackStatus(trackId).RequirementTitle}.",
                CareerRaceRequestStatus.FinaleLocked => $"The final is locked. Requires: {GetFinaleStatus().NextMissingTitle}.",
                CareerRaceRequestStatus.UnknownTrack => "Unknown circuit.",
                _ => string.Empty
            };
        }

        private RaceStartCheckResult VehicleCheck()
        {
            return _selectedVehicle.HasCommittedVehicleSelection && !IsVehicleOwned(_selectedVehicle.SelectedVehicleId)
                ? RaceStartCheckResult.Reject("The selected car is not owned.")
                : RaceStartCheckResult.Allow();
        }

        private PendingRace CreateNormalRace(string sceneName)
        {
            var index = _tracks.IndexOfScene(sceneName);
            return index >= 0 ? new PendingRace(_tracks.Tracks[index].TrackId, sceneName, false) : null;
        }

        private int CountAchievedMilestones()
        {
            var count = 0;
            foreach (var milestone in _configuration.Milestones)
            {
                if (_state.HasAchieved(milestone.MilestoneId)) count++;
            }

            return count;
        }

        private int CountOwnedVehicles()
        {
            var count = 0;
            foreach (var vehicle in _vehicles.GetRaceSelectableVehicles())
            {
                if (IsVehicleOwned(vehicle.StableId)) count++;
            }

            return count;
        }

        private int CountUnlockedTracks()
        {
            var count = 0;
            foreach (var track in _tracks.Tracks)
            {
                if (!track.FinaleOnly && IsTrackUnlocked(track.TrackId)) count++;
            }

            return count;
        }

        private HashSet<string> SnapshotUnlockedTracks()
        {
            var unlocked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var track in _tracks.Tracks)
            {
                if (!track.FinaleOnly && IsTrackUnlocked(track.TrackId)) unlocked.Add(track.TrackId);
            }

            return unlocked;
        }

        private HashSet<string> SnapshotPurchasableVehicles()
        {
            var purchasable = new HashSet<string>(StringComparer.Ordinal);
            foreach (var offer in _configuration.VehicleOffers)
            {
                if (!IsVehicleOwned(offer.VehicleId) && GetVehicleStatus(offer.VehicleId).PrerequisiteMet) purchasable.Add(offer.VehicleId);
            }

            return purchasable;
        }

        private List<string> NewTrackNames(HashSet<string> before)
        {
            var names = new List<string>();
            foreach (var trackId in SnapshotUnlockedTracks())
            {
                if (!before.Contains(trackId)) names.Add(GetTrackName(trackId));
            }

            return names;
        }

        private List<string> NewVehicleNames(HashSet<string> before)
        {
            var names = new List<string>();
            foreach (var vehicleId in SnapshotPurchasableVehicles())
            {
                if (!before.Contains(vehicleId)) names.Add(GetVehicleName(vehicleId));
            }

            return names;
        }

        private CareerRaceOutcome Outcome(CareerRaceOutcomeStatus status, string trackId, bool isFinale, bool won)
        {
            var summary = GetSummary();
            return new CareerRaceOutcome(status, trackId, isFinale, won, null, null, null, summary.CompletionPercent, summary.CompletionPercent, false, summary);
        }

        private CareerVehiclePurchaseResult PurchaseResult(CareerVehiclePurchaseStatus status, string vehicleId, int price, IReadOnlyList<CareerMilestoneAward> awards, string message)
        {
            return new CareerVehiclePurchaseResult(status, vehicleId, price, _progression.Currency, awards, message);
        }

        private static CareerRaceRequestResult RaceRequest(CareerRaceRequestStatus status, string trackId, bool isFinale, string message)
        {
            return new CareerRaceRequestResult(status, trackId, string.Empty, isFinale, message);
        }

        private sealed class PendingRace
        {
            public PendingRace(string trackId, string sceneName, bool isFinale)
            {
                TrackId = trackId;
                SceneName = sceneName;
                IsFinale = isFinale;
            }

            public string TrackId { get; }
            public string SceneName { get; }
            public bool IsFinale { get; }
        }
    }
}
