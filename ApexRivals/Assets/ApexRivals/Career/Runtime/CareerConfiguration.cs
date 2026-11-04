using System;
using System.Collections.Generic;
using System.Linq;
using ApexRivals.Progression.Runtime;
using ApexRivals.Tracks.Runtime;
using ApexRivals.VehicleSelection.Runtime;

namespace ApexRivals.Career.Runtime
{
    /// <summary>
    /// Immutable career rules: the starter car, car prices, circuit unlocks, the ordered milestone list and
    /// the final. <see cref="Validate"/> checks it against the real catalogs before the career is used.
    /// </summary>
    public sealed class CareerConfiguration
    {
        private readonly Dictionary<string, CareerMilestone> _milestonesById = new Dictionary<string, CareerMilestone>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerVehicleOffer> _offersByVehicleId = new Dictionary<string, CareerVehicleOffer>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerTrackUnlock> _unlocksByTrackId = new Dictionary<string, CareerTrackUnlock>(StringComparer.Ordinal);

        public CareerConfiguration(
            string starterVehicleId,
            IReadOnlyList<CareerVehicleOffer> vehicleOffers,
            IReadOnlyList<CareerTrackUnlock> trackUnlocks,
            IReadOnlyList<CareerMilestone> milestones,
            CareerFinale finale)
        {
            StarterVehicleId = starterVehicleId?.Trim() ?? string.Empty;
            VehicleOffers = Copy(vehicleOffers);
            TrackUnlocks = Copy(trackUnlocks);
            Milestones = Copy(milestones);
            Finale = finale;

            foreach (var milestone in Milestones)
            {
                if (milestone != null && !string.IsNullOrWhiteSpace(milestone.MilestoneId) && !_milestonesById.ContainsKey(milestone.MilestoneId))
                {
                    _milestonesById.Add(milestone.MilestoneId, milestone);
                }
            }

            foreach (var offer in VehicleOffers)
            {
                if (offer != null && !string.IsNullOrWhiteSpace(offer.VehicleId) && !_offersByVehicleId.ContainsKey(offer.VehicleId))
                {
                    _offersByVehicleId.Add(offer.VehicleId, offer);
                }
            }

            foreach (var unlock in TrackUnlocks)
            {
                if (unlock != null && !string.IsNullOrWhiteSpace(unlock.TrackId) && !_unlocksByTrackId.ContainsKey(unlock.TrackId))
                {
                    _unlocksByTrackId.Add(unlock.TrackId, unlock);
                }
            }
        }

        public string StarterVehicleId { get; }
        public IReadOnlyList<CareerVehicleOffer> VehicleOffers { get; }
        public IReadOnlyList<CareerTrackUnlock> TrackUnlocks { get; }

        /// <summary>Milestones in objective order; the first one not achieved is the active objective.</summary>
        public IReadOnlyList<CareerMilestone> Milestones { get; }
        public CareerFinale Finale { get; }

        public CareerMilestone FindMilestone(string milestoneId)
        {
            return !string.IsNullOrWhiteSpace(milestoneId) && _milestonesById.TryGetValue(milestoneId, out var milestone) ? milestone : null;
        }

        public CareerVehicleOffer FindOffer(string vehicleId)
        {
            return !string.IsNullOrWhiteSpace(vehicleId) && _offersByVehicleId.TryGetValue(vehicleId, out var offer) ? offer : null;
        }

        public CareerTrackUnlock FindTrackUnlock(string trackId)
        {
            return !string.IsNullOrWhiteSpace(trackId) && _unlocksByTrackId.TryGetValue(trackId, out var unlock) ? unlock : null;
        }

        public CareerValidationResult Validate(VehicleCatalog vehicles, TrackCatalog tracks, IReadOnlyDictionary<UpgradeType, int> maximumUpgradeLevels)
        {
            if (vehicles == null || tracks == null)
            {
                return CareerValidationResult.Failure("Career validation needs the vehicle and track catalogs.");
            }

            var starter = vehicles.FindById(StarterVehicleId);
            if (!starter.Succeeded || !VehicleCatalog.IsRaceSelectable(starter.Vehicle))
            {
                return CareerValidationResult.Failure($"Career starter vehicle '{StarterVehicleId}' is not a raceable catalog vehicle.");
            }

            var offerResult = ValidateOffers(vehicles);
            if (!offerResult.Succeeded) return offerResult;

            var trackResult = ValidateTrackUnlocks(tracks);
            if (!trackResult.Succeeded) return trackResult;

            var milestoneResult = ValidateMilestones(vehicles, tracks, maximumUpgradeLevels);
            if (!milestoneResult.Succeeded) return milestoneResult;

            var finaleResult = ValidateFinale(tracks);
            if (!finaleResult.Succeeded) return finaleResult;

            return ValidateReachability(tracks, maximumUpgradeLevels);
        }

        private CareerValidationResult ValidateOffers(VehicleCatalog vehicles)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var offer in VehicleOffers)
            {
                if (offer == null || string.IsNullOrWhiteSpace(offer.VehicleId))
                {
                    return CareerValidationResult.Failure("A career vehicle offer has no vehicle ID.");
                }

                if (!seen.Add(offer.VehicleId))
                {
                    return CareerValidationResult.Failure($"Career vehicle offer '{offer.VehicleId}' is duplicated.");
                }

                if (offer.VehicleId == StarterVehicleId)
                {
                    return CareerValidationResult.Failure("The starter vehicle is granted and must not be offered for sale.");
                }

                var lookup = vehicles.FindById(offer.VehicleId);
                if (!lookup.Succeeded || !VehicleCatalog.IsRaceSelectable(lookup.Vehicle))
                {
                    return CareerValidationResult.Failure($"Career vehicle offer '{offer.VehicleId}' is not a raceable catalog vehicle.");
                }

                if (offer.Price <= 0)
                {
                    return CareerValidationResult.Failure($"Career vehicle '{offer.VehicleId}' needs a positive price.");
                }

                if (!string.IsNullOrWhiteSpace(offer.RequiredMilestoneId) && FindMilestone(offer.RequiredMilestoneId) == null)
                {
                    return CareerValidationResult.Failure($"Career vehicle '{offer.VehicleId}' requires unknown milestone '{offer.RequiredMilestoneId}'.");
                }
            }

            foreach (var vehicle in vehicles.GetRaceSelectableVehicles())
            {
                if (vehicle.StableId != StarterVehicleId && FindOffer(vehicle.StableId) == null)
                {
                    return CareerValidationResult.Failure($"Raceable vehicle '{vehicle.StableId}' has no career price.");
                }
            }

            return CareerValidationResult.Valid();
        }

        private CareerValidationResult ValidateTrackUnlocks(TrackCatalog tracks)
        {
            if (tracks.Count == 0)
            {
                return CareerValidationResult.Failure("The career needs at least one circuit.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unlock in TrackUnlocks)
            {
                if (unlock == null || string.IsNullOrWhiteSpace(unlock.TrackId))
                {
                    return CareerValidationResult.Failure("A career circuit unlock has no track ID.");
                }

                if (!seen.Add(unlock.TrackId))
                {
                    return CareerValidationResult.Failure($"Career circuit unlock '{unlock.TrackId}' is duplicated.");
                }

                if (tracks.IndexOf(unlock.TrackId) < 0)
                {
                    return CareerValidationResult.Failure($"Career circuit unlock references unknown track '{unlock.TrackId}'.");
                }

                if (string.IsNullOrWhiteSpace(unlock.RequiredMilestoneId) || FindMilestone(unlock.RequiredMilestoneId) == null)
                {
                    return CareerValidationResult.Failure($"Circuit '{unlock.TrackId}' requires unknown milestone '{unlock.RequiredMilestoneId}'.");
                }
            }

            var initialTrack = tracks.DefaultTrack;
            if (initialTrack == null || FindTrackUnlock(initialTrack.TrackId) != null)
            {
                return CareerValidationResult.Failure("The initial circuit must be available without requirements.");
            }

            return CareerValidationResult.Valid();
        }

        private CareerValidationResult ValidateMilestones(VehicleCatalog vehicles, TrackCatalog tracks, IReadOnlyDictionary<UpgradeType, int> maximumUpgradeLevels)
        {
            if (Milestones.Count == 0)
            {
                return CareerValidationResult.Failure("The career has no milestones.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var finaleCount = 0;
            for (var index = 0; index < Milestones.Count; index++)
            {
                var milestone = Milestones[index];
                if (milestone == null || string.IsNullOrWhiteSpace(milestone.MilestoneId))
                {
                    return CareerValidationResult.Failure("A career milestone has no ID.");
                }

                if (!seen.Add(milestone.MilestoneId))
                {
                    return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' is duplicated.");
                }

                if (string.IsNullOrWhiteSpace(milestone.Title))
                {
                    return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' has no title.");
                }

                if (milestone.CurrencyReward < 0)
                {
                    return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' has a negative reward.");
                }

                switch (milestone.Kind)
                {
                    case CareerMilestoneKind.CompleteTrack:
                    case CareerMilestoneKind.WinTrack:
                        if (tracks.IndexOf(milestone.TargetId) < 0)
                        {
                            return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' targets unknown track '{milestone.TargetId}'.");
                        }

                        break;
                    case CareerMilestoneKind.ReachUpgradeLevel:
                        if (!TryParseUpgrade(milestone.TargetId, out var upgradeType)
                            || maximumUpgradeLevels == null
                            || !maximumUpgradeLevels.TryGetValue(upgradeType, out var maximumLevel)
                            || milestone.TargetCount < 1
                            || milestone.TargetCount > maximumLevel)
                        {
                            return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' has an invalid upgrade target.");
                        }

                        break;
                    case CareerMilestoneKind.OwnVehicles:
                        if (milestone.TargetCount < 1 || milestone.TargetCount > vehicles.GetRaceSelectableVehicles().Count)
                        {
                            return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' asks for more cars than exist.");
                        }

                        break;
                    case CareerMilestoneKind.UnlockTracks:
                        if (milestone.TargetCount < 1 || milestone.TargetCount > tracks.Tracks.Count(track => !track.FinaleOnly))
                        {
                            return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' asks for more circuits than exist.");
                        }

                        break;
                    case CareerMilestoneKind.WinFinale:
                        finaleCount++;
                        if (index != Milestones.Count - 1)
                        {
                            return CareerValidationResult.Failure("The final milestone must be the last milestone.");
                        }

                        break;
                    default:
                        return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' has an unknown type.");
                }
            }

            return finaleCount == 1
                ? CareerValidationResult.Valid()
                : CareerValidationResult.Failure("The career needs exactly one final milestone.");
        }

        private CareerValidationResult ValidateFinale(TrackCatalog tracks)
        {
            if (Finale == null || tracks.IndexOf(Finale.TrackId) < 0)
            {
                return CareerValidationResult.Failure("The career final must be held on a known circuit.");
            }

            if (string.IsNullOrWhiteSpace(Finale.Title))
            {
                return CareerValidationResult.Failure("The career final has no title.");
            }

            if (Finale.RequiredMilestoneIds.Count == 0)
            {
                return CareerValidationResult.Failure("The career final must be gated by earlier milestones.");
            }

            foreach (var milestoneId in Finale.RequiredMilestoneIds)
            {
                var milestone = FindMilestone(milestoneId);
                if (milestone == null)
                {
                    return CareerValidationResult.Failure($"The career final requires unknown milestone '{milestoneId}'.");
                }

                if (milestone.Kind == CareerMilestoneKind.WinFinale)
                {
                    return CareerValidationResult.Failure("The career final cannot require itself.");
                }
            }

            return CareerValidationResult.Valid();
        }

        /// <summary>
        /// Plays the rules forward from a fresh career with unlimited races: every milestone must become
        /// achievable, so no requirement chain (circuit, car or final) can lock the career.
        /// Money is not simulated because every finished race pays.
        /// </summary>
        private CareerValidationResult ValidateReachability(TrackCatalog tracks, IReadOnlyDictionary<UpgradeType, int> maximumUpgradeLevels)
        {
            var achieved = new HashSet<string>(StringComparer.Ordinal);
            bool changed;
            do
            {
                changed = false;
                foreach (var milestone in Milestones)
                {
                    if (!achieved.Contains(milestone.MilestoneId) && CanBeReached(milestone, achieved, tracks, maximumUpgradeLevels))
                    {
                        achieved.Add(milestone.MilestoneId);
                        changed = true;
                    }
                }
            }
            while (changed);

            foreach (var milestone in Milestones)
            {
                if (!achieved.Contains(milestone.MilestoneId))
                {
                    return CareerValidationResult.Failure($"Career milestone '{milestone.MilestoneId}' can never be reached.");
                }
            }

            return CareerValidationResult.Valid();
        }

        private bool CanBeReached(CareerMilestone milestone, HashSet<string> achieved, TrackCatalog tracks, IReadOnlyDictionary<UpgradeType, int> maximumUpgradeLevels)
        {
            switch (milestone.Kind)
            {
                case CareerMilestoneKind.CompleteTrack:
                case CareerMilestoneKind.WinTrack:
                    return IsTrackOpen(milestone.TargetId, achieved);
                case CareerMilestoneKind.ReachUpgradeLevel:
                    return TryParseUpgrade(milestone.TargetId, out var type) && maximumUpgradeLevels.TryGetValue(type, out var maximum) && milestone.TargetCount <= maximum;
                case CareerMilestoneKind.OwnVehicles:
                    var buyable = 1;
                    foreach (var offer in VehicleOffers)
                    {
                        if (string.IsNullOrWhiteSpace(offer.RequiredMilestoneId) || achieved.Contains(offer.RequiredMilestoneId))
                        {
                            buyable++;
                        }
                    }

                    return buyable >= milestone.TargetCount;
                case CareerMilestoneKind.UnlockTracks:
                    var open = 0;
                    foreach (var track in tracks.Tracks)
                    {
                        if (!track.FinaleOnly && IsTrackOpen(track.TrackId, achieved)) open++;
                    }

                    return open >= milestone.TargetCount;
                case CareerMilestoneKind.WinFinale:
                    foreach (var required in Finale.RequiredMilestoneIds)
                    {
                        if (!achieved.Contains(required)) return false;
                    }

                    return IsTrackOpen(Finale.TrackId, achieved);
                default:
                    return false;
            }
        }

        private bool IsTrackOpen(string trackId, HashSet<string> achieved)
        {
            var unlock = FindTrackUnlock(trackId);
            return unlock == null || achieved.Contains(unlock.RequiredMilestoneId);
        }

        public static bool TryParseUpgrade(string value, out UpgradeType upgradeType)
        {
            upgradeType = default;
            return !string.IsNullOrWhiteSpace(value)
                && Enum.TryParse(value.Trim(), true, out upgradeType)
                && Enum.IsDefined(typeof(UpgradeType), upgradeType);
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null)
            {
                return Array.Empty<T>();
            }

            var copy = new T[source.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
