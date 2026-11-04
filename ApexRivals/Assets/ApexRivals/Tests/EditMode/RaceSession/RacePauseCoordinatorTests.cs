using ApexRivals.RaceSession.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.RaceSession
{
    public sealed class RacePauseCoordinatorTests
    {
        private float _original;
        private RacePauseCoordinator _coordinator;
        [SetUp] public void SetUp()
        {
            _original = Time.timeScale;
            Time.timeScale = 0.4f;
            _coordinator = new RacePauseCoordinator(new UnityTimeScaleController());
        }
        [TearDown] public void TearDown()
        {
            _coordinator.Dispose();
            Time.timeScale = _original;
        }

        [Test]
        public void FallbackCannotReleaseLifecyclePause()
        {
            var session = _coordinator.CreateSession();
            session.TryOpenFallbackPause();
            session.Pause();
            session.CloseFallbackPause();
            Assert.That(Time.timeScale, Is.Zero);
            session.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(0.4f));
        }

        [Test]
        public void LifecycleResumeCannotReleaseOpenFallback()
        {
            var session = _coordinator.CreateSession();
            session.Pause();
            session.TryOpenFallbackPause();
            session.Resume();
            Assert.That(Time.timeScale, Is.Zero);
            session.CloseFallbackPause();
            Assert.That(Time.timeScale, Is.EqualTo(0.4f));
        }

        [Test]
        public void PreviousSessionCannotReleaseOrPauseItsReplacement()
        {
            var previous = _coordinator.CreateSession();
            previous.TryOpenFallbackPause();
            var replacement = _coordinator.CreateSession();
            replacement.Pause();
            previous.CloseFallbackPause();
            previous.Resume();
            previous.Dispose();
            Assert.That(Time.timeScale, Is.Zero);
            replacement.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(0.4f));
            previous.Pause();
            Assert.That(previous.TryOpenFallbackPause(), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.4f));
        }

        [Test]
        public void FallbackReleasePreservesANewerPositiveDecision()
        {
            var session = _coordinator.CreateSession();
            session.TryOpenFallbackPause();
            Time.timeScale = 0.7f;
            session.CloseFallbackPause();
            Assert.That(Time.timeScale, Is.EqualTo(0.7f));
        }

        [Test]
        public void ApplicationDisposalReleasesClockAndInvalidatesSessionRequests()
        {
            var session = _coordinator.CreateSession();
            session.TryOpenFallbackPause();
            _coordinator.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(0.4f));
            Time.timeScale = 0.8f;
            session.CloseFallbackPause();
            session.Pause();
            session.Resume();
            session.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
        }
    }
}
