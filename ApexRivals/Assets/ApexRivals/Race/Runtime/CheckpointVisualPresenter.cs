using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    // Presentation-only. Shows exactly one checkpoint's VisualMarker at a time - the
    // human player's NextExpectedCheckpointIndex, read directly from RaceCoordinator/
    // RacerProgress (the sole authoritative progression state; this component owns no
    // progression state of its own and never calls TryPassCheckpoint or otherwise
    // influences gameplay). AI participants' checkpoint progress is intentionally
    // ignored - every handler below filters events by the player's own RacerId before
    // touching the active marker.
    //
    // Entirely event-driven off RaceCoordinator's existing events (RaceStarted,
    // CheckpointPassed, RacerFinished): no Update loop, no polling, no
    // FindObjectOfType/GameObject.Find/GetComponent-in-Update.
    [DisallowMultipleComponent]
    public sealed class CheckpointVisualPresenter : MonoBehaviour
    {
        [SerializeField]
        private RaceCoordinator raceCoordinator;

        [Header("Debug (read-only, updated on progress refresh)")]
        [SerializeField]
        private int debugCurrentLap;

        [SerializeField]
        private int debugLastValidCheckpoint;

        [SerializeField]
        private int debugExpectedCheckpoint;

        private int _activeIndex = -1;

        public int ActiveCheckpointIndex => _activeIndex;

        public void Configure(RaceCoordinator coordinator)
        {
            if (isActiveAndEnabled)
            {
                Unsubscribe();
            }

            raceCoordinator = coordinator;

            if (isActiveAndEnabled)
            {
                Subscribe();
                RefreshFromProgress();
            }
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshFromProgress();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (raceCoordinator == null)
            {
                return;
            }

            raceCoordinator.RaceStarted += OnRaceStarted;
            raceCoordinator.CheckpointPassed += OnCheckpointPassed;
            raceCoordinator.RacerFinished += OnRacerFinished;
        }

        private void Unsubscribe()
        {
            if (raceCoordinator == null)
            {
                return;
            }

            raceCoordinator.RaceStarted -= OnRaceStarted;
            raceCoordinator.CheckpointPassed -= OnCheckpointPassed;
            raceCoordinator.RacerFinished -= OnRacerFinished;
        }

        private void OnRaceStarted(RaceStartedEvent raceEvent)
        {
            RefreshFromProgress();
        }

        private void OnCheckpointPassed(CheckpointPassedEvent raceEvent)
        {
            var player = raceCoordinator != null ? raceCoordinator.PlayerParticipant : null;
            if (player == null || raceEvent.RacerId != player.RacerId)
            {
                // AI (or any non-player) progress must never move the player's marker.
                return;
            }

            RefreshFromProgress();
        }

        private void OnRacerFinished(RacerFinishedEvent raceEvent)
        {
            var player = raceCoordinator != null ? raceCoordinator.PlayerParticipant : null;
            if (player == null || raceEvent.Result.RacerId != player.RacerId)
            {
                return;
            }

            SetActiveIndex(-1);
        }

        private void RefreshFromProgress()
        {
            if (raceCoordinator == null)
            {
                SetActiveIndex(-1);
                return;
            }

            var player = raceCoordinator.PlayerParticipant;
            if (player == null || !raceCoordinator.TryGetProgress(player, out var progress))
            {
                SetActiveIndex(-1);
                return;
            }

            debugCurrentLap = progress.CurrentLap;
            debugLastValidCheckpoint = progress.LastValidCheckpointIndex;
            debugExpectedCheckpoint = progress.NextExpectedCheckpointIndex;

            var checkpointCount = raceCoordinator.OrderedCheckpoints != null ? raceCoordinator.OrderedCheckpoints.Count : 0;
            SetActiveIndex(ResolveActiveIndex(true, true, progress.HasFinished, progress.NextExpectedCheckpointIndex, checkpointCount));
        }

        // Pure decision logic, kept free of MonoBehaviour/UnityEngine state so it can
        // be unit tested directly. Returns the checkpoint index that should be shown
        // active, or -1 if nothing should be shown.
        public static int ResolveActiveIndex(bool hasPlayer, bool hasProgress, bool hasFinished, int nextExpectedCheckpointIndex, int checkpointCount)
        {
            if (!hasPlayer || !hasProgress || hasFinished || checkpointCount <= 0)
            {
                return -1;
            }

            if (nextExpectedCheckpointIndex < 0 || nextExpectedCheckpointIndex >= checkpointCount)
            {
                return -1;
            }

            return nextExpectedCheckpointIndex;
        }

        private void SetActiveIndex(int index)
        {
            if (_activeIndex == index)
            {
                return;
            }

            var checkpoints = raceCoordinator != null ? raceCoordinator.OrderedCheckpoints : null;
            if (checkpoints != null)
            {
                if (_activeIndex >= 0 && _activeIndex < checkpoints.Count && checkpoints[_activeIndex] != null)
                {
                    checkpoints[_activeIndex].SetVisualActive(false);
                }

                if (index >= 0 && index < checkpoints.Count && checkpoints[index] != null)
                {
                    checkpoints[index].SetVisualActive(true);
                }
            }

            _activeIndex = index;
        }
    }
}
