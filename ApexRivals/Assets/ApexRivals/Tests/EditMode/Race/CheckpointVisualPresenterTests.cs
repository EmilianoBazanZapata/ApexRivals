using System;
using System.Collections.Generic;
using System.Reflection;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Race
{
    public sealed class CheckpointVisualPresenterTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            _spawned.Clear();
        }

        // --- Pure decision-logic tests (no GameObjects required) --------------

        [Test]
        public void ResolveActiveIndex_NoPlayer_ReturnsHidden()
        {
            var index = CheckpointVisualPresenter.ResolveActiveIndex(false, true, false, 0, 4);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void ResolveActiveIndex_Finished_ReturnsHidden()
        {
            var index = CheckpointVisualPresenter.ResolveActiveIndex(true, true, true, 0, 4);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void ResolveActiveIndex_ValidExpectedIndex_ReturnsIt()
        {
            var index = CheckpointVisualPresenter.ResolveActiveIndex(true, true, false, 2, 4);
            Assert.That(index, Is.EqualTo(2));
        }

        [Test]
        public void ResolveActiveIndex_OutOfRangeIndex_ReturnsHidden()
        {
            var index = CheckpointVisualPresenter.ResolveActiveIndex(true, true, false, 9, 4);
            Assert.That(index, Is.EqualTo(-1));
        }

        // --- Integration tests against real RaceCoordinator/RaceParticipant/RaceCheckpoint ---

        [Test]
        public void RaceStart_FirstExpectedCheckpoint_BecomesActive()
        {
            var scenario = BuildScenario(4);

            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(0));
            Assert.That(scenario.Visuals[0].activeSelf, Is.True);
            for (var i = 1; i < scenario.Visuals.Length; i++)
            {
                Assert.That(scenario.Visuals[i].activeSelf, Is.False);
            }
        }

        [Test]
        public void RaceStart_StartFinishConsumed_PlayerAndAiExpectCheckpointOneWithoutCompletingALap()
        {
            var scenario = BuildScenario(4, initiallyConsumedCheckpointIndex: 0);

            AssertInitialProgress(scenario.Player, scenario.Coordinator);
            AssertInitialProgress(scenario.Ai, scenario.Coordinator);
            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(1));
            Assert.That(scenario.Visuals[0].activeSelf, Is.False);
            Assert.That(scenario.Visuals[1].activeSelf, Is.True);
        }

        [Test]
        public void RaceStart_StartFinishConsumed_DoesNotNeedCheckpointZeroTriggerReentry()
        {
            var scenario = BuildScenario(4, initiallyConsumedCheckpointIndex: 0);

            Assert.That(scenario.Coordinator.TryGetProgress(scenario.Player, out var progress), Is.True);
            Assert.That(progress.LastValidCheckpointIndex, Is.EqualTo(0));
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(1));
        }

        [Test]
        public void PlayerPassesExpectedCheckpoint_ActiveMarkerAdvances_PreviousDeactivates()
        {
            var scenario = BuildScenario(4);

            scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[0]);

            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(1));
            Assert.That(scenario.Visuals[0].activeSelf, Is.False);
            Assert.That(scenario.Visuals[1].activeSelf, Is.True);
        }

        [Test]
        public void AiPassesCheckpoint_PlayerMarkerDoesNotChange()
        {
            var scenario = BuildScenario(4);
            scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[0]);
            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(1));

            // AI has its own independent progress and legitimately needs checkpoint 0 too.
            scenario.Coordinator.TryPassCheckpoint(scenario.Ai, scenario.Checkpoints[0]);

            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(1), "AI progress must never move the player's marker.");
            Assert.That(scenario.Visuals[1].activeSelf, Is.True);
        }

        [Test]
        public void SkippedCheckpoint_DoesNotAdvanceMarker()
        {
            var scenario = BuildScenario(4);
            scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[0]);
            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(1));

            // Player is expected at checkpoint 1 but attempts to enter checkpoint 2 directly.
            var result = scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[2]);

            Assert.That(result.Status, Is.EqualTo(CheckpointPassStatus.Skipped));
            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(1), "Checkpoint 1 must remain the visibly required checkpoint after a skip attempt.");
            Assert.That(scenario.Visuals[1].activeSelf, Is.True);
            Assert.That(scenario.Visuals[2].activeSelf, Is.False);
        }

        [Test]
        public void LapTransition_ActivatesFirstCheckpointOfNextLap()
        {
            var scenario = BuildScenario(4);

            scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[0]);
            scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[1]);
            scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[2]);
            var result = scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[3]);

            Assert.That(result.LapCompleted, Is.True);
            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(0), "Checkpoint 0 must become the active marker for the next lap.");
            Assert.That(scenario.Visuals[3].activeSelf, Is.False, "The previous lap's last marker must not remain active.");
            Assert.That(scenario.Visuals[0].activeSelf, Is.True);
        }

        [Test]
        public void StartFinishAfterFinalRouteCheckpoint_CompletesLapAndReturnsExpectationToCheckpointOne()
        {
            var scenario = BuildScenario(3, initiallyConsumedCheckpointIndex: 0);

            Assert.That(scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[1]).Accepted, Is.True);
            Assert.That(scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[2]).Accepted, Is.True);
            var finish = scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[0]);

            Assert.That(finish.LapCompleted, Is.True);
            Assert.That(finish.RacerFinished, Is.False);
            Assert.That(scenario.Coordinator.TryGetProgress(scenario.Player, out var progress), Is.True);
            Assert.That(progress.CompletedLaps, Is.EqualTo(1));
            Assert.That(progress.CurrentLap, Is.EqualTo(2));
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(1));
        }

        [Test]
        public void ThirdStartFinishCrossing_AfterEachRoute_FinishesThreeLapRace()
        {
            var scenario = BuildScenario(3, initiallyConsumedCheckpointIndex: 0);
            CheckpointPassResult finish = default;

            for (var lap = 0; lap < 3; lap++)
            {
                Assert.That(scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[1]).Accepted, Is.True);
                Assert.That(scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[2]).Accepted, Is.True);
                finish = scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[0]);
            }

            Assert.That(finish.LapCompleted, Is.True);
            Assert.That(finish.RacerFinished, Is.True);
            Assert.That(scenario.Coordinator.TryGetProgress(scenario.Player, out var progress), Is.True);
            Assert.That(progress.CompletedLaps, Is.EqualTo(3));
            Assert.That(progress.HasFinished, Is.True);
        }

        [Test]
        public void RegisteringExistingRacer_DoesNotReapplyInitialCheckpointConsumption()
        {
            var scenario = BuildScenario(4, initiallyConsumedCheckpointIndex: 0);
            scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[1]);

            scenario.Coordinator.RegisterRacer(scenario.Player);

            Assert.That(scenario.Coordinator.TryGetProgress(scenario.Player, out var progress), Is.True);
            Assert.That(progress.LastValidCheckpointIndex, Is.EqualTo(1));
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(2));
            Assert.That(progress.CurrentLap, Is.EqualTo(1));
        }

        [Test]
        public void RaceFinish_HidesAllMarkers()
        {
            var scenario = BuildScenario(4);

            // Default TotalLaps fallback (no RaceDefinition asset) is 3 - complete all 3.
            for (var lap = 0; lap < 3; lap++)
            {
                for (var checkpointIndex = 0; checkpointIndex < 4; checkpointIndex++)
                {
                    scenario.Coordinator.TryPassCheckpoint(scenario.Player, scenario.Checkpoints[checkpointIndex]);
                }
            }

            Assert.That(scenario.Presenter.ActiveCheckpointIndex, Is.EqualTo(-1), "No checkpoint should be shown as required after the player finishes the race.");
            foreach (var visual in scenario.Visuals)
            {
                Assert.That(visual.activeSelf, Is.False);
            }
        }

        // --- Scenario setup -----------------------------------------------

        private sealed class Scenario
        {
            public RaceCoordinator Coordinator;
            public CheckpointVisualPresenter Presenter;
            public RaceParticipant Player;
            public RaceParticipant Ai;
            public RaceCheckpoint[] Checkpoints;
            public GameObject[] Visuals;
        }

        private Scenario BuildScenario(int checkpointCount, int initiallyConsumedCheckpointIndex = -1)
        {
            var coordinatorGo = NewGameObject("RaceCoordinator");
            var coordinator = coordinatorGo.AddComponent<RaceCoordinator>();

            var checkpoints = new RaceCheckpoint[checkpointCount];
            var visuals = new GameObject[checkpointCount];
            for (var i = 0; i < checkpointCount; i++)
            {
                var checkpointGo = NewGameObject($"Checkpoint_{i}");
                var box = checkpointGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                var checkpoint = checkpointGo.AddComponent<RaceCheckpoint>();

                var visualGo = NewGameObject($"VisualMarker_{i}");
                visualGo.transform.SetParent(checkpointGo.transform, false);
                visualGo.SetActive(false);

                var checkpointSo = new SerializedObject(checkpoint);
                checkpointSo.FindProperty("checkpointIndex").intValue = i;
                checkpointSo.FindProperty("raceCoordinator").objectReferenceValue = coordinator;
                checkpointSo.FindProperty("visualMarker").objectReferenceValue = visualGo;
                checkpointSo.ApplyModifiedPropertiesWithoutUndo();

                checkpoints[i] = checkpoint;
                visuals[i] = visualGo;
            }

            var coordinatorSo = new SerializedObject(coordinator);
            coordinatorSo.FindProperty("initiallyConsumedCheckpointIndex").intValue = initiallyConsumedCheckpointIndex;
            var ordered = coordinatorSo.FindProperty("orderedCheckpoints");
            ordered.arraySize = checkpoints.Length;
            for (var i = 0; i < checkpoints.Length; i++)
            {
                ordered.GetArrayElementAtIndex(i).objectReferenceValue = checkpoints[i];
            }

            coordinatorSo.ApplyModifiedPropertiesWithoutUndo();

            var player = NewParticipant("Player");
            var ai = NewParticipant("Ai_01");

            coordinator.ConfigureSession(new[] { player, ai }, Array.Empty<DrivingInputGate>(), player);

            // Bypass the real countdown/Update loop (not driven automatically in
            // EditMode tests) - directly place the coordinator in the Racing state,
            // which is the only precondition TryPassCheckpoint actually checks.
            typeof(RaceCoordinator)
                .GetField("state", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(coordinator, RaceState.Racing);

            var presenterGo = coordinatorGo;
            var presenter = presenterGo.AddComponent<CheckpointVisualPresenter>();
            presenter.Configure(coordinator);

            return new Scenario
            {
                Coordinator = coordinator,
                Presenter = presenter,
                Player = player,
                Ai = ai,
                Checkpoints = checkpoints,
                Visuals = visuals
            };
        }

        private static void AssertInitialProgress(RaceParticipant participant, RaceCoordinator coordinator)
        {
            Assert.That(coordinator.TryGetProgress(participant, out var progress), Is.True);
            Assert.That(progress.LastValidCheckpointIndex, Is.EqualTo(0));
            Assert.That(progress.NextExpectedCheckpointIndex, Is.EqualTo(1));
            Assert.That(progress.CurrentLap, Is.EqualTo(1));
            Assert.That(progress.CompletedLaps, Is.Zero);
        }

        private RaceParticipant NewParticipant(string racerId)
        {
            var go = NewGameObject($"Participant_{racerId}");
            var participant = go.AddComponent<RaceParticipant>();
            participant.Configure(null, racerId, go.transform);
            return participant;
        }

        private GameObject NewGameObject(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }
    }
}
