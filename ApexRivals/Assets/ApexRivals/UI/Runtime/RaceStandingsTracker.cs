using System;
using System.Collections.Generic;
using System.Globalization;
using ApexRivals.Race.Runtime;

namespace ApexRivals.UI.Runtime
{
    /// <summary>One row of the HUD standings tower.</summary>
    public readonly struct RaceStandingEntry
    {
        public RaceStandingEntry(string racerId, int position, string name, string gap, bool isPlayer)
        {
            RacerId = racerId;
            Position = position;
            Name = name;
            Gap = gap;
            IsPlayer = isPlayer;
        }

        public string RacerId { get; }
        public int Position { get; }
        public string Name { get; }
        public string Gap { get; }
        public bool IsPlayer { get; }
    }

    /// <summary>One row of the results classification.</summary>
    public readonly struct RaceClassificationEntry
    {
        public RaceClassificationEntry(int position, string name, string time, string bestLap, bool isPlayer)
        {
            Position = position;
            Name = name;
            Time = time;
            BestLap = bestLap;
            IsPlayer = isPlayer;
        }

        public int Position { get; }
        public string Name { get; }

        /// <summary>Finish time, or the interval to the player for a racer still on track.</summary>
        public string Time { get; }
        public string BestLap { get; }
        public bool IsPlayer { get; }
    }

    /// <summary>
    /// Live race timing for the HUD, derived from the coordinator's checkpoint events: the player's current and best
    /// lap, how much of the race the player has completed, and the running order with each rival's interval to the
    /// player, measured at the last checkpoint both have passed (the way race broadcasts time intervals).
    /// </summary>
    public sealed class RaceStandingsTracker : IDisposable
    {
        public const string NoGap = "---";

        private readonly RaceCoordinator _coordinator;
        private readonly Dictionary<string, List<float>> _checkpointTimes = new Dictionary<string, List<float>>(StringComparer.Ordinal);
        private readonly List<RaceParticipant> _ordered = new List<RaceParticipant>();
        private readonly Dictionary<string, float> _lapStartByRacer = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _bestLapByRacer = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _finishTimeByRacer = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<RaceStandingEntry> _standingsScratch = new List<RaceStandingEntry>();
        private float _lapStartTime;
        private float _bestLapTime = -1f;
        private float _finalLapTime = -1f;
        private bool _disposed;

        public RaceStandingsTracker(RaceCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _coordinator.RaceStarted += OnRaceStarted;
            _coordinator.CheckpointPassed += OnCheckpointPassed;
            _coordinator.LapCompleted += OnLapCompleted;
            _coordinator.RacerFinished += OnRacerFinished;
        }

        /// <summary>Time on the lap in progress; after the finish it keeps showing the last lap.</summary>
        public float CurrentLapTime
        {
            get
            {
                if (_finalLapTime >= 0f) return _finalLapTime;
                return _coordinator.State == RaceState.Racing ? Math.Max(0f, _coordinator.ElapsedRaceTime - _lapStartTime) : 0f;
            }
        }

        /// <summary>Fastest completed lap, or a negative value before the first lap is completed.</summary>
        public float BestLapTime => _bestLapTime;

        /// <summary>Share of the race the player has completed (checkpoints passed over every checkpoint of every lap).</summary>
        public float RaceProgress
        {
            get
            {
                var player = _coordinator.PlayerParticipant;
                if (player == null || !_coordinator.TryGetProgress(player, out var progress)) return 0f;
                if (progress.HasFinished) return 1f;
                var total = _coordinator.OrderedCheckpoints.Count * _coordinator.TotalLaps;
                return total > 0 ? Math.Min(1f, Math.Max(0f, progress.TotalPassedCheckpoints / (float)total)) : 0f;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _coordinator.RaceStarted -= OnRaceStarted;
            _coordinator.CheckpointPassed -= OnCheckpointPassed;
            _coordinator.LapCompleted -= OnLapCompleted;
            _coordinator.RacerFinished -= OnRacerFinished;
        }

        /// <summary>
        /// Final classification for the results screen: running order with each finisher's time (a racer still on
        /// track shows its interval to the player instead) and every racer's best lap.
        /// </summary>
        public void GetClassification(List<RaceClassificationEntry> classification)
        {
            classification.Clear();
            GetStandings(_standingsScratch);
            for (var index = 0; index < _standingsScratch.Count; index++)
            {
                var standing = _standingsScratch[index];
                var time = TryGetFinishTime(standing.RacerId, out var finish)
                    ? UguiViewText.FormatTime(finish)
                    : standing.Gap;
                var best = _bestLapByRacer.TryGetValue(standing.RacerId, out var lap)
                    ? UguiViewText.FormatTime(lap)
                    : "--:--.---";
                classification.Add(new RaceClassificationEntry(standing.Position, standing.Name, time, best, standing.IsPlayer));
            }
        }

        /// <summary>Fills <paramref name="standings"/> with every racer in running order.</summary>
        public void GetStandings(List<RaceStandingEntry> standings)
        {
            standings.Clear();
            _ordered.Clear();
            var participants = _coordinator.Participants;
            for (var index = 0; index < participants.Count; index++)
            {
                if (participants[index] != null) _ordered.Add(participants[index]);
            }

            _ordered.Sort((left, right) => PositionOf(left).CompareTo(PositionOf(right)));
            var player = _coordinator.PlayerParticipant;
            var playerId = player != null ? player.RacerId : null;
            var playerPosition = player != null ? _coordinator.GetLivePosition(playerId) : 0;
            for (var index = 0; index < _ordered.Count; index++)
            {
                var racer = _ordered[index];
                var isPlayer = racer == player;
                var position = _coordinator.GetLivePosition(racer.RacerId);
                standings.Add(new RaceStandingEntry(
                    racer.RacerId,
                    position > 0 ? position : index + 1,
                    isPlayer ? "YOU" : DisplayName(racer.RacerId),
                    isPlayer || playerId == null ? NoGap : Gap(racer.RacerId, playerId, position > 0 && position < playerPosition),
                    isPlayer));
            }
        }

        /// <summary>Driver names for the AI rivals, by their number: AI_01 is the first name, AI_02 the second...</summary>
        private static readonly string[] RivalDriverNames =
        {
            "M. VEGA", "K. SATO", "R. HAYES", "L. MORETTI", "J. NAKAMURA", "D. COLE", "A. PETROV", "S. LINDQVIST"
        };

        /// <summary>
        /// "AI_01" reads as its driver name ("M. VEGA"); a rival numbered past the roster falls back to "RIVAL 9",
        /// and any other id is shown upper-cased.
        /// </summary>
        public static string DisplayName(string racerId)
        {
            if (string.IsNullOrEmpty(racerId)) return string.Empty;
            var digits = racerId.Length;
            while (digits > 0 && char.IsDigit(racerId[digits - 1])) digits--;
            if (racerId.StartsWith("AI", StringComparison.OrdinalIgnoreCase) && digits < racerId.Length
                && int.TryParse(racerId.Substring(digits), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return number >= 1 && number <= RivalDriverNames.Length
                    ? RivalDriverNames[number - 1]
                    : "RIVAL " + number.ToString(CultureInfo.InvariantCulture);
            }

            return racerId.Replace('_', ' ').ToUpperInvariant();
        }

        public static string FormatGap(float seconds)
        {
            seconds = Math.Max(0f, seconds);
            if (seconds < 60f) return "+" + seconds.ToString("0.000", CultureInfo.InvariantCulture);
            var minutes = (int)(seconds / 60f);
            return "+" + minutes.ToString(CultureInfo.InvariantCulture) + ":" + (seconds - minutes * 60f).ToString("00.000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Finish time from the coordinator's results, which are recorded before RacerFinished is raised: the results
        /// screen can render from an earlier subscriber of that event, before this tracker hears about it.
        /// </summary>
        private bool TryGetFinishTime(string racerId, out float finishTime)
        {
            var results = _coordinator.Results;
            for (var index = 0; index < results.Count; index++)
            {
                if (results[index].RacerId == racerId)
                {
                    finishTime = results[index].FinishTime;
                    return true;
                }
            }

            return _finishTimeByRacer.TryGetValue(racerId, out finishTime);
        }

        private int PositionOf(RaceParticipant participant)
        {
            var position = _coordinator.GetLivePosition(participant.RacerId);
            return position > 0 ? position : int.MaxValue;
        }

        /// <summary>
        /// Interval between a rival and the player. When one of them has already passed the checkpoint the other is
        /// heading to, the interval is the time since that pass, so it counts up live until the other one gets
        /// there; otherwise it is the difference at the last checkpoint both have passed.
        /// </summary>
        private string Gap(string racerId, string playerId, bool rivalAhead)
        {
            var rival = TimesOf(racerId);
            var player = TimesOf(playerId);
            var now = _coordinator.ElapsedRaceTime;
            if (rival.Count > player.Count) return FormatGap(now - rival[player.Count]);
            if (player.Count > rival.Count) return FormatGap(now - player[rival.Count]);
            var common = rival.Count;
            if (common == 0) return NoGap;
            var interval = rivalAhead ? player[common - 1] - rival[common - 1] : rival[common - 1] - player[common - 1];
            return FormatGap(Math.Abs(interval));
        }

        private List<float> TimesOf(string racerId)
        {
            if (!_checkpointTimes.TryGetValue(racerId, out var times))
            {
                times = new List<float>();
                _checkpointTimes.Add(racerId, times);
            }

            return times;
        }

        private void OnRaceStarted(RaceStartedEvent raceStarted)
        {
            _checkpointTimes.Clear();
            _lapStartByRacer.Clear();
            _bestLapByRacer.Clear();
            _finishTimeByRacer.Clear();
            _lapStartTime = 0f;
            _bestLapTime = -1f;
            _finalLapTime = -1f;
        }

        private void OnCheckpointPassed(CheckpointPassedEvent passed)
        {
            TimesOf(passed.RacerId).Add(_coordinator.ElapsedRaceTime);
        }

        private void OnRacerFinished(RacerFinishedEvent finished)
        {
            _finishTimeByRacer[finished.Result.RacerId] = finished.Result.FinishTime;
        }

        private void OnLapCompleted(LapCompletedEvent lap)
        {
            var lapNow = _coordinator.ElapsedRaceTime;
            var lapStart = _lapStartByRacer.TryGetValue(lap.RacerId, out var start) ? start : 0f;
            var racerLap = lapNow - lapStart;
            _bestLapByRacer[lap.RacerId] = _bestLapByRacer.TryGetValue(lap.RacerId, out var bestSoFar) ? Math.Min(bestSoFar, racerLap) : racerLap;
            _lapStartByRacer[lap.RacerId] = lapNow;

            var player = _coordinator.PlayerParticipant;
            if (player == null || lap.RacerId != player.RacerId) return;

            var now = _coordinator.ElapsedRaceTime;
            var lapTime = now - _lapStartTime;
            _bestLapTime = _bestLapTime < 0f ? lapTime : Math.Min(_bestLapTime, lapTime);
            _lapStartTime = now;
            if (lap.CompletedLap >= lap.TotalLaps) _finalLapTime = lapTime;
        }
    }
}
