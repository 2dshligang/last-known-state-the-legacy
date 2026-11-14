using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationEchoTests
    {
        [Test]
        public void ReplaySeeksAcrossSimultaneousRecordsAndMissingIntervals()
        {
            var host = new GameObject("ECHO test");
            try
            {
                var run = Start(host, out var clock);
                var echo = run.Echo;
                Assert.That(StationEcho.Timeline[1].Evidence.RecordedAt,
                    Is.EqualTo(StationEcho.Timeline[2].Evidence.RecordedAt));
                Assert.That(StationEcho.Timeline.Select(frame => frame.Sequence),
                    Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
                Assert.That(StationEcho.Timeline.Any(frame => frame.Evidence.Kind == EvidenceKind.Observation),
                    Is.False);

                echo.Restart();
                clock.Seconds = 9;
                run.Tick();
                Assert.That(echo.Snapshot.Frame.Evidence.Quality, Is.EqualTo(EvidenceQuality.Missing));
                Assert.That(echo.Snapshot.Frame.Evidence.RecordedAt, Is.Null);
                echo.Seek(4);
                Assert.That(echo.Snapshot.Frame.Evidence.Id.ToString(), Is.EqualTo("echo.backup-estimate"));
                echo.Stop();
                clock.Seconds = 11;
                run.Tick();
                Assert.That(echo.Snapshot.Seconds, Is.Zero);
                Assert.That(echo.Snapshot.Playing, Is.False);
                echo.Restart();
                clock.Seconds = 35;
                run.Tick();
                Assert.That(echo.Snapshot.Seconds, Is.EqualTo(StationEcho.DurationSeconds));
                Assert.That(echo.Snapshot.Playing, Is.False);
                Assert.That(run.CaptureQuestCheckpoint(), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void SeekBurstsCoalesceAndDisposedSubscriptionsReceiveNothing()
        {
            var host = new GameObject("ECHO test");
            try
            {
                var run = Start(host, out _);
                int deliveries = 0;
                using var active = run.Echo.Subscribe(change =>
                {
                    deliveries++;
                    Assert.That(change.Snapshot.Seconds, Is.EqualTo(5));
                });
                for (int index = 0; index < 100; index++)
                {
                    using var temporary = run.Echo.Subscribe(_ => Assert.Fail("Disposed observer called."));
                    run.Echo.Seek(index % 2 + 4);
                }
                Assert.That(deliveries, Is.Zero);
                run.Dispatch();
                run.Dispatch();
                Assert.That(deliveries, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static GameplaySession Start(GameObject host, out Clock clock)
        {
            var bootstrap = host.AddComponent<SessionBootstrap>();
            clock = new Clock();
            Assert.That(bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable, clock: clock), Is.Empty);
            return bootstrap.Current;
        }

        private sealed class Clock : ISessionClock { public double Seconds { get; set; } }
        private sealed class Catalog : IScienceCatalog
        {
            public bool TryGet(StableId id, out ScienceRecord record)
            {
                record = default;
                return false;
            }
        }
    }
}
