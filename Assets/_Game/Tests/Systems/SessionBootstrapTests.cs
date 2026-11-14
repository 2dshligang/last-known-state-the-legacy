using System;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class SessionBootstrapTests
    {
        private GameObject host;
        private SessionBootstrap bootstrap;
        private ManualClock clock;
        private FixtureCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Session bootstrap test");
            bootstrap = host.AddComponent<SessionBootstrap>();
            clock = new ManualClock { Seconds = 10 };
            catalog = new FixtureCatalog();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void RestartDropsPendingCallbacksAndResetsTimeAndRegistry()
        {
            var oldTarget = new Target();
            Start(oldTarget);
            var previous = bootstrap.Current;
            int oldCalls = 0;
            using var subscription = previous.Subscribe(_ => oldCalls++);
            clock.Seconds = 12;
            previous.Tick();
            Assert.That(previous.Snapshot, Is.EqualTo(2));

            var newTarget = new Target();
            Start(newTarget);
            var current = bootstrap.Current;
            Assert.Throws<ObjectDisposedException>(() => previous.Dispatch());
            Assert.That(current.Snapshot, Is.Zero);
            Assert.That(current.TryGetTarget(newTarget.Id, out var bound), Is.True);
            Assert.That(bound, Is.SameAs(newTarget));

            int newCalls = 0;
            using var currentSubscription = current.Subscribe(change =>
            {
                Assert.That(change.Sequence, Is.EqualTo(1));
                Assert.That(change.Snapshot, Is.EqualTo(3));
                newCalls++;
            });
            clock.Seconds = 15;
            current.Tick();
            Assert.That(newCalls, Is.Zero);
            current.Dispatch();
            current.Tick();
            current.Dispatch();
            Assert.That(newCalls, Is.EqualTo(1));
            Assert.That(oldCalls, Is.Zero);

            bootstrap.StopSession();
            Assert.That(bootstrap.Current, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => current.Tick());
        }

        [Test]
        public void DisposedObserverIsSkippedDuringDispatch()
        {
            Start(new Target());
            var session = bootstrap.Current;
            IDisposable second = null;
            using var first = session.Subscribe(_ => second.Dispose());
            second = session.Subscribe(_ => Assert.Fail("Disposed observer was called."));
            clock.Seconds++;
            session.Tick();
            session.Dispatch();
        }

        [Test]
        public void FailingObserverDoesNotDropNotificationsForOtherObservers()
        {
            Start(new Target());
            var session = bootstrap.Current;
            using var failing = session.Subscribe(_ => throw new InvalidOperationException("Observer failed."));
            var received = new System.Collections.Generic.List<long>();
            using var healthy = session.Subscribe(change => received.Add(change.Sequence));

            clock.Seconds++;
            session.Tick();
            clock.Seconds++;
            session.Tick();
            var error = Assert.Throws<AggregateException>(() => session.Dispatch());

            Assert.That(error.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(received, Is.EqualTo(new long[] { 1, 2 }));
            Assert.DoesNotThrow(() => session.Dispatch());
        }

        [Test]
        public void DisabledBootstrapRejectsStartBeforeCreatingTargets()
        {
            host.SetActive(false);
            var errors = bootstrap.Restart(catalog, new[] { FixtureCatalog.Id },
                () => throw new InvalidOperationException("Disabled bootstrap invoked the factory."), clock);

            Assert.That(errors, Is.EqualTo(new[] { "Enable the session bootstrap before starting the session." }));
            Assert.That(bootstrap.Current, Is.Null);
            host.SetActive(true);
            Start(new Target());
            Assert.That(bootstrap.Current.Snapshot, Is.Zero);
        }

        [Test]
        public void InvalidConfigurationReportsMissingDependenciesBeforeCreatingTargets()
        {
            var errors = bootstrap.Restart(null, null, null, clock);
            Assert.That(errors, Has.Count.EqualTo(3));
            Assert.That(bootstrap.Current, Is.Null);

            errors = bootstrap.Restart(catalog, new[] { new StableId("record.missing") },
                () => throw new InvalidOperationException("Factory ran before validation."), clock);
            Assert.That(errors[0], Does.Contain("record.missing"));
            Assert.That(bootstrap.Current, Is.Null);
        }

        [Test]
        public void DuplicateTargetsLeaveExistingSessionActive()
        {
            Start(new Target());
            var previous = bootstrap.Current;
            var errors = bootstrap.Restart(catalog, Array.Empty<StableId>(),
                () => new[] { new Target(), new Target() }, clock);
            Assert.That(errors[0], Does.Contain("Duplicate interaction target ID"));
            Assert.That(bootstrap.Current, Is.SameAs(previous));
        }

        private void Start(Target target)
        {
            var errors = bootstrap.Restart(catalog, new[] { FixtureCatalog.Id },
                () => new[] { target }, clock);
            Assert.That(errors, Is.Empty);
        }

        private sealed class ManualClock : ISessionClock
        {
            public double Seconds { get; set; }
        }

        private sealed class FixtureCatalog : IScienceCatalog
        {
            internal static readonly StableId Id = new StableId("record.fixture");

            public bool TryGet(StableId id, out ScienceRecord record)
            {
                record = id == Id ? new ScienceRecord(Id, EvidenceKind.Fiction, null, null) : default;
                return id == Id;
            }
        }

        private sealed class Target : IInteractable
        {
            public StableId Id { get; } = new StableId("target.fixture");
            public InteractionState Read(StableId actor) => default;
            public CommandResult Request(StableId actor, StableId action) =>
                new CommandResult(ResultCode.Unavailable, new StableId("result.unavailable"));
        }
    }
}
